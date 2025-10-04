try {
    $xml = [xml](Get-Content 'e:\wii\code\TeknoParrotUI_CN\TeknoParrotUi\Properties\Resources.zh-TW.resx')
    Write-Host "XML file format is correct."
    Exit 0
} catch {
    Write-Host "XML file format error: $_"
    Exit 1
}