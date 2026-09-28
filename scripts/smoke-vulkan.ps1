param([Parameter(Mandatory = $true)][string]$ModelPath)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$model = (Resolve-Path -LiteralPath $ModelPath).Path
$output = Join-Path $repo ('artifacts/smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
& powershell.exe -NoProfile -File (Join-Path $PSScriptRoot 'create-synthetic-speech.ps1') -OutputDirectory $output
if ($LASTEXITCODE -ne 0) { throw 'Synthetic speech generation failed.' }
$media = Join-Path $output 'three-streams.mkv'
& ffmpeg -hide_banner -loglevel error -i (Join-Path $output 'microphone.wav') -i (Join-Path $output 'desktop.wav') -filter_complex '[0:a][1:a]amix=inputs=2:duration=longest[mix]' -map '[mix]' -map 0:a -map 1:a -c:a pcm_s16le $media
if ($LASTEXITCODE -ne 0) { throw 'FFmpeg fixture generation failed.' }
& dotnet build (Join-Path $repo 'src/WhisperTranscriber') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$log = Join-Path $output 'vulkan.log'
& dotnet run --project (Join-Path $repo 'src/WhisperTranscriber') -c Release --no-build -- $media --multi-track --model-path $model --language en --output-dir $output *> $log
if ($LASTEXITCODE -ne 0) { throw "GPU smoke test failed. Inspect $log" }
$logText = Get-Content -LiteralPath $log -Raw
if ($logText -notmatch 'using Vulkan\d+ backend') { throw 'No active Vulkan backend found in native log.' }
$srt = Get-Content -LiteralPath (Join-Path $output 'three-streams_Transcript_AMD_Mehrspur_2Sprecher.srt') -Raw
if ($srt -notmatch '\[Mikrofon\]' -or $srt -notmatch '\[Desktop\]' -or $srt -notmatch '-->') {
    throw 'Missing track labels or subtitle timing.'
}
[pscustomobject]@{Passed = $true; Output = $output; Scope = 'Native Vulkan, two audio streams, labeled SRT'}
