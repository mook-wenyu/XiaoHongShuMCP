Param(
  [Parameter(Mandatory=$true)][string]$ToolName,
  [string]$Json,
  [string]$File
)

$argsList = @('callTool', $ToolName)
if ($Json) { $argsList += @('--json', $Json) }
if ($File) { $argsList += @('--file', $File) }

dotnet run --project XiaoHongShuMCP --% $argsList

