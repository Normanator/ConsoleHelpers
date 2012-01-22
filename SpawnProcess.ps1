function SpawnProcess
{
        param( [string] $target  = $(throw "Please specify an executable target"),
                [string] $argText,
                [string] $workDir,
                [int]    $timeoutSec, 
                [Switch] $WhatIf,
                [Switch] $Confirm
            )

        if( $Confirm )
        {
                if( !(ConfirmAction "Spawn '$target' '$argText'?") )
                {
                        return
                }
        }
        if( $WhatIf )
        {
                write-host "WhatIf: Spawning '$target' '$argText'"
                return
        }


        $errText     = ""
        $outText     = ""

        [Diagnostics.ProcessStartInfo] $startInfo =
                New-Object Diagnostics.ProcessStartInfo
        $startInfo.UseShellExecute               = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError  = $true
        $startInfo.Filename               = $target
        $startInfo.Arguments              = $argText
        $startInfo.WorkingDirectory       = $workDir


        [Diagnostics.Process] $proc       = New-Object Diagnostics.Process
        $proc.StartInfo                   = $startInfo

        # N.B. Seems we can't use $proc.add_ErrorDataReceived
        # as $proc.BeginErrorReadLine( ) crashes the powershell runspace!
        # q.v. http://www.codeguru.com/forum/showthread.php?t=450974&goto=nextoldest 
        $StdErrBuffer = New-Object -Type byte[] -ArgumentList 65535
        $StdOutBuffer = New-Object -Type byte[] -ArgumentList 65535

        $proc.Start( )| Out-Null

        $StdErrReadResult = $proc.StandardError.BaseStream.BeginRead(
                $StdErrBuffer, 0, $StdErrBuffer.Length, $null, $null)
        $StdOutReadResult = $proc.StandardOutput.BaseStream.BeginRead(
                $StdOutBuffer, 0, $StdOutBuffer.Length, $null, $null)

        $sleepMsecs   = 50
        if( $timeoutSec -le 0 ) { $timeOutLoops = [int]::MaxValue }
        else {     $timeOutLoops = ($timeoutSecs * 1000) / $sleepMsecs }
        while( !($proc.HasExited) -and (--$timeOutLoops -gt 0)  )
        {          
                [System.Threading.Thread]::Sleep( $sleepMsecs )

                # -- If the buffers get full, drain 'em and rewind
                if( $StdErrReadResult.IsCompleted )
                {
                        $count    = $proc.StandardError.BaseStream.EndRead( $StdErrReadResult )
                        $errText += [System.Text.Encoding]::UTF8.GetString( $StdErrBuffer, 0, $count)
                        $StdErrReadResult = $proc.StandardError.BaseStream.BeginRead(
                                $StdErrBuffer, 0, $StdErrBuffer.Length, $null, $null)
                }
                if( $StdOutReadResult.IsCompleted )
                {
                        $count    = $proc.StandardOutput.BaseStream.EndRead( $StdOutReadResult )
                        $outText += [System.Text.Encoding]::UTF8.GetString( $StdOutBuffer, 0, $count )
                        $StdOutReadResult = $proc.StandardOutput.BaseStream.BeginRead(
                                $StdOutBuffer, 0, $StdOutBuffer.Length, $null, $null)
                }
        }

        $count    = $proc.StandardError.BaseStream.EndRead( $StdErrReadResult )
        $errText += [System.Text.Encoding]::UTF8.GetString( $StdErrBuffer, 0, $count)
        $count    = $proc.StandardOutput.BaseStream.EndRead( $StdOutReadResult )
        $outText += [System.Text.Encoding]::UTF8.GetString( $StdOutBuffer, 0, $count )

        if( $timeoutLoops -le 0 )
        {
                $proc.Close()
                throw "Spawned process timed-out ($timeoutSecs sec)"
        }


        $ecode = [int] $proc.ExitCode
        if( $ecode -ne 0 )
        {
                $proc.Close()
                throw "spawned process failed (exit:$ecode) -- " + $errText + " -- " +$outText
        }

        # Return the output of the process
        $outText
}