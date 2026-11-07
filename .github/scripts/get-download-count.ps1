param(
    [Parameter(Mandatory)] [string] $Repository # owner/name
)

# Total downloads of latest.zip across all releases (needs GH_TOKEN for the gh CLI). Dalamud shows this as the plugin's download count.
# The jq filter has no quotes on purpose: Windows PowerShell 5.1 strips embedded quotes from native arguments.
$rows = gh api "repos/$Repository/releases" --paginate --jq '.[].assets[] | [.name, .download_count] | @tsv'
$total = 0
foreach ($row in $rows)
{
    $name, $count = $row -split "`t"
    if ($name -eq 'latest.zip') { $total += [int]$count }
}
$total
