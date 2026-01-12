$lines = Get-Content 'c:\Users\krish.maniar\Desktop\Template-Builder\Template-Builder\Views\Home\Index.cshtml' -TotalCount 3
Write-Host "First 3 lines:"
for ($i=0; $i -lt $lines.Count; $i++) {
    Write-Host "Line $($i+1): '$($lines[$i])'"
}
