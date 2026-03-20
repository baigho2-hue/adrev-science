
param(
  [string]$SourceDir,
  [string]$OutputFile,
  [string]$ComponentGroupId,
  [string]$DirectoryId
)

$wixContent = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
"@

$componentRefs = @()
$directoryXml = ""

function Process-Directory {
  param (
    [string]$CurrentDir,
    [string]$ParentId,
    [string]$SourceRoot
  )

  $items = Get-ChildItem -Path $CurrentDir
  $files = $items | Where-Object { -not $_.PSIsContainer } | Where-Object { $_.Name -ne "AdRev.Desktop.exe" -and $_.Name -ne "AdRev.Desktop.pdb" }
  $subdirs = $items | Where-Object { $_.PSIsContainer }

  # Process Files
  foreach ($file in $files) {
    # Relative path for ID generation
    $relPath = $file.FullName.Substring($SourceRoot.Length).TrimStart("\").TrimStart("/")
    $safeName = "_" + $relPath.Replace("\", "_").Replace("/", "_").Replace(".", "_").Replace("-", "_").Replace(" ", "_").Replace("+", "_")
    $compId = "Comp" + $safeName
    $fileId = "File" + $safeName
    $fileSource = $file.FullName

    # Add to component refs
    $script:componentRefs += "      <ComponentRef Id=""$compId"" />"

    # Add component definition
    $script:directoryXml += @"
        <Component Id="$compId">
          <File Id="$fileId" Source="$fileSource" KeyPath="yes" />
        </Component>
"@
  }

  # Process Subdirectories
  foreach ($subdir in $subdirs) {
    $dirName = $subdir.Name
    # Unique ID for directory
    $relPath = $subdir.FullName.Substring($SourceRoot.Length).TrimStart("\").TrimStart("/")
    $dirId = "Dir_" + $relPath.Replace("\", "_").Replace("/", "_").Replace(".", "_").Replace("-", "_").Replace(" ", "_").Replace("+", "_")
        
    $script:directoryXml += "`n        <Directory Id=""$dirId"" Name=""$dirName"">`n"
    Process-Directory -CurrentDir $subdir.FullName -ParentId $dirId -SourceRoot $SourceRoot
    $script:directoryXml += "        </Directory>`n"
  }
}

# Start processing
$script:directoryXml += "    <DirectoryRef Id=""$DirectoryId"">`n"
Process-Directory -CurrentDir $SourceDir -ParentId $DirectoryId -SourceRoot $SourceDir
$script:directoryXml += "    </DirectoryRef>`n"

$wixContent += $script:directoryXml

$wixContent += @"
    <ComponentGroup Id="$ComponentGroupId">
$($script:componentRefs -join "`n")
    </ComponentGroup>
  </Fragment>
</Wix>
"@

$wixContent | Out-File -FilePath $OutputFile -Encoding UTF8
Write-Host "Generated WiX fragment at $OutputFile with $($files.Count) components."
