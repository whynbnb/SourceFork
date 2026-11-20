Remove-Item -Path build\SourceFork\*.pdb -Force
Compress-Archive -Path build\SourceFork -DestinationPath "build\sourcefork_${env:VERSION}.${env:RUNTIME}.zip" -Force