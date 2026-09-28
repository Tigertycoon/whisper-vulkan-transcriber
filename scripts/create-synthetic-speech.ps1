param([Parameter(Mandatory = $true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
# Run with Windows PowerShell 5.1, which includes the System.Speech desktop assembly.
Add-Type -AssemblyName System.Speech
$directory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voices = @($synth.GetInstalledVoices() | Where-Object {$_.Enabled} | ForEach-Object {$_.VoiceInfo.Name})
if ($voices.Count -eq 0) { throw 'Install a Windows desktop speech synthesis voice first.' }
try {
    $synth.SelectVoice($voices[0])
    $synth.SetOutputToWaveFile((Join-Path $directory 'microphone.wav'))
    $synth.Speak('This is a local software test. The microphone track contains the first speaker. No private recording is used.')
    $synth.SetOutputToNull()
    $synth.SelectVoice($voices[[Math]::Min(1, $voices.Count - 1)])
    $synth.SetOutputToWaveFile((Join-Path $directory 'desktop.wav'))
    $synth.Speak('The desktop track contains the second speaker. We are testing subtitles and audio stream selection.')
    $synth.SetOutputToNull()
}
finally { $synth.Dispose() }
