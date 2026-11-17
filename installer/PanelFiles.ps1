# MSI owns every runtime file, so repair, upgrades and removal use standard component rules.
param([Parameter(Mandatory)][string]$directory)
$ErrorActionPreference='Stop'
$directory=(Resolve-Path -LiteralPath $directory).Path
$xml=New-Object System.Xml.XmlDocument
$ns='http://wixtoolset.org/schemas/v4/wxs'
function Element($parent,$name,$attributes) {
    $node=$xml.CreateElement($name,$ns)
    foreach($key in $attributes.Keys){$node.SetAttribute($key,[string]$attributes[$key])}
    [void]$parent.AppendChild($node)
    $node
}
$root=Element $xml Wix @{}
$frag=Element $root Fragment @{}
$group=Element $frag ComponentGroup @{Id='PanelRuntime';Directory='PANELDIR'}
$sha=[Security.Cryptography.SHA256]::Create()
try {
    foreach($file in (Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object Extension -ne '.pdb')){
        $relative=$file.FullName.Substring($directory.Length+1)
        $id=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($relative.ToLowerInvariant()))).Replace('-','').Substring(0,24)
        $attributes=@{Id="C$id";Guid='*'}
        $sub=[IO.Path]::GetDirectoryName($relative)
        if($sub){$attributes.Subdirectory=$sub}
        $component=Element $group Component $attributes
        [void](Element $component File @{Id="F$id";Source=$file.FullName;KeyPath='yes'})
    }
} finally {$sha.Dispose()}
$xml.Save((Join-Path (Resolve-Path payload).Path 'PanelFiles.wxs'))
