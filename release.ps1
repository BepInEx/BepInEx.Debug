# Env setup ---------------
if ($PSScriptRoot -match '.+?\\bin\\?') {
    $dir = $PSScriptRoot + "\"
}
else {
    $dir = $PSScriptRoot + "\bin\"
}

$copy = $dir + "\copy\BepInEx\plugins" 
$plugins = $dir + "\Release"

# Create releases ---------
function CreateZip ($pluginFile)
{
    Remove-Item -Force -Path ($dir + "\copy") -Recurse -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $copy

    Copy-Item -Path $pluginFile.FullName -Destination $copy -Recurse -Force 

    # the replace removes .0 from the end of version up until it hits a non-0 or there are only 2 version parts remaining (e.g. v1.0 v1.0.1)
    $ver = (Get-ChildItem -Path ($copy) -Filter "*.dll" -Recurse -Force)[0].VersionInfo.FileVersion.ToString() -replace "^([\d+\.]+?\d+)[\.0]*$", '${1}'

    Compress-Archive -Path ($copy + "\..\") -Force -CompressionLevel "Optimal" -DestinationPath ($dir + $pluginFile.BaseName + "_" + "r" + $ver + ".zip")
}

foreach ($pluginFile in Get-ChildItem -Path $plugins) 
{
    try
    {
        CreateZip ($pluginFile)
    }
    catch 
    {
        # retry
        CreateZip ($pluginFile)
    }
}

Remove-Item -Force -Path ($dir + "\copy") -Recurse

# Create Starup profiler release
$profilerdir = $dir + "\..\src\SimpleProfiler\bin"

Get-ChildItem -Path ($profilerdir) | Where{$_.Name -Match "^MonoProfiler(32|64)\.(?!dll)"} | Remove-Item

$ver = (Get-ChildItem -Path $profilerdir -Filter "MonoProfilerController.dll" -Recurse -Force)[0].VersionInfo.FileVersion.ToString() -replace "^([\d+\.]+?\d+)[\.0]*$", '${1}'

Compress-Archive -Path ($profilerdir + "\*") -Force -CompressionLevel "Optimal" -DestinationPath ($dir + "SimpleMonoProfiler_" + "r" + $ver + ".zip")
