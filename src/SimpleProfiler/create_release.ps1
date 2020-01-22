if ($PSScriptRoot -match '.+?\\bin\\?') {
    $dir = $PSScriptRoot + "\"
}
else {
    $dir = $PSScriptRoot + "\bin\"
}

Get-ChildItem -Path ($dir) | Where{$_.Name -Match "^MonoProfiler(32|64)\.(?!dll)"} | Remove-Item

$ver = "v" + (Get-ChildItem -Path ($dir + "\BepInEx\plugins\") -Filter "*.dll" -Recurse -Force)[0].VersionInfo.FileVersion.ToString()

Compress-Archive -Path ($dir + "\*") -Force -CompressionLevel "Optimal" -DestinationPath ($dir + "..\SimpleMonoProfiler_" + $ver + ".zip")
