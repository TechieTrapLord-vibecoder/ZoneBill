$path = 'C:\Users\hmmth\Downloads\IT15_PROJECT FINAL DOCS-V2 (1).docx'
if (Test-Path $path) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
    $entry = $zip.Entries | Where-Object { $_.FullName -eq 'word/document.xml' }
    if ($entry) {
        $stream = $entry.Open()
        $reader = New-Object System.IO.StreamReader($stream)
        $xmlText = $reader.ReadToEnd()
        $reader.Close()
        $stream.Close()
        
        [xml]$doc = $xmlText
        $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
        $ns.AddNamespace('w', 'http://schemas.openxmlformats.org/wordprocessingml/2006/main')
        
        $nodes = $doc.SelectNodes('//w:t', $ns)
        $textContent = ($nodes | ForEach-Object { $_.InnerText }) -join " "
        Write-Output $textContent
    } else {
        Write-Error "Could not find word/document.xml in the docx file."
    }
    $zip.Dispose()
} else {
    Write-Error "File not found at $path"
}
