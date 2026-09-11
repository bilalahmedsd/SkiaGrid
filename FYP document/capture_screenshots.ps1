Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing,System.Windows.Forms
$OUT = "D:\Bilal\SkiaGrid\FYP document\screenshots"
if (Test-Path $OUT) { Remove-Item "$OUT\*.png" -Force }
New-Item -ItemType Directory -Force $OUT | Out-Null
$exe = "D:\Bilal\SkiaGrid\SampleApplicationV2\bin\Debug\net8.0-windows\SampleApplicationV2.exe"

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$INV = [System.Windows.Automation.InvokePattern]::Pattern
$SEL = [System.Windows.Automation.SelectionItemPattern]::Pattern
$TOG = [System.Windows.Automation.TogglePattern]::Pattern

$script:pid0 = 0
function MainWin {
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $script:pid0)
  $all = $AE::RootElement.FindAll($TS::Children, $c)
  foreach ($w in $all) { if ($w.Current.Name -like "*Trading Demo Showcase*") { return $w } }
  if ($all.Count -gt 0) { return $all[0] }
  return $null
}
function ByName($name) {
  $w = MainWin
  if ($null -eq $w) { return $null }
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
  return $w.FindFirst($TS::Descendants, $c)
}
function Click($name) {
  $e = ByName $name
  if ($e) { try { $e.GetCurrentPattern($INV).Invoke(); return } catch { } }
  Write-Output "  ! invoke failed: '$name'"
}
function SelectTab($name) {
  $e = ByName $name
  if ($e) { try { $e.GetCurrentPattern($SEL).Select(); return } catch { } }
  Write-Output "  ! tab select failed: '$name'"
}
function Toggle($name) {
  $e = ByName $name
  if ($e) { try { $e.GetCurrentPattern($TOG).Toggle(); return } catch { } }
  Write-Output "  ! toggle failed: '$name'"
}
function Shot($file) {
  # Always capture the whole virtual screen: the app runs maximized, and a window-relative
  # grab silently produces a 0x0 bitmap the moment the UIA element goes stale.
  $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
  $bmp.Save("$OUT\$file", [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  $alive = if (Get-Process -Id $script:pid0 -ErrorAction SilentlyContinue) { "alive" } else { "DEAD" }
  Write-Output "  saved $file  (app $alive)"
}

$p = Start-Process $exe -PassThru
$script:pid0 = $p.Id
Start-Sleep -Seconds 14
if ($null -eq (MainWin)) { Write-Output "NO WINDOW"; exit 1 }
Write-Output ("window: " + (MainWin).Current.Name)

Write-Output "01 stress 100k + auto-scroll"
Click "100,000"; Start-Sleep -Seconds 6
Click "Auto-scroll"; Start-Sleep -Seconds 8
Shot "01_stress_100k.png"

Write-Output "02 stress 100k + live feed"
Toggle "Live ticks (2,000 rows / 100 ms)"; Start-Sleep -Seconds 9
Shot "02_stress_100k_livefeed.png"

Write-Output "03 stress 1M"
Toggle "Live ticks (2,000 rows / 100 ms)"; Start-Sleep -Seconds 2
Click "Stop scrolling"; Start-Sleep -Seconds 2
Click "1,000,000"; Start-Sleep -Seconds 12
Click "Auto-scroll"; Start-Sleep -Seconds 8
Shot "03_stress_1million.png"
Click "Stop scrolling"; Start-Sleep -Seconds 2

Write-Output "04 all features - tree rows"
SelectTab "All Features"; Start-Sleep -Seconds 5
Click "ExpandAllRows()"; Start-Sleep -Seconds 2
Click "GetExpandedRowItems()"; Start-Sleep -Seconds 1
Shot "04_all_features_tree.png"

Write-Output "05 all features - grouping + 6 aggregations"
Click "CollapseAllRows()"; Start-Sleep -Seconds 1
Click "ApplyGroup(Category) + 6 aggregations"; Start-Sleep -Seconds 3
Click "Read expand state"; Start-Sleep -Seconds 1
Click "GetAllPercentiles()"; Start-Sleep -Seconds 2
Shot "05_all_features_grouped.png"

Write-Output "06 all features - header theme + filter + density"
Click "ClearGroup()"; Start-Sleep -Seconds 1
Toggle "Custom header colours (Background / Foreground / Separator)"; Start-Sleep -Seconds 1
Click "AddValueFilter Price > 0"; Start-Sleep -Seconds 1
Click "AddTextFilter Name = Item 1*"; Start-Sleep -Seconds 2
Shot "06_all_features_theme_filter.png"

Write-Output "07 performance dashboard"
SelectTab "Performance"; Start-Sleep -Seconds 14
Shot "07_performance_dashboard.png"

Write-Output "08 collectionview - feature breadth"
SelectTab "CollectionView"; Start-Sleep -Seconds 7
Shot "08_collectionview_features.png"

Write-Output "09 collectionview - grouped"
Click "Apply"; Start-Sleep -Seconds 5
Shot "09_collectionview_grouped.png"

Write-Output "10 scroll bar gutter"
SelectTab "Scroll Bar Gutter"; Start-Sleep -Seconds 5
Click "Fill exactly"; Start-Sleep -Seconds 3
Shot "10_scrollbar_gutter.png"

Write-Output "11 row drag + drop target"
SelectTab "Row Drag"; Start-Sleep -Seconds 5
Click "Open drop-target window"; Start-Sleep -Seconds 5
Shot "11_row_drag.png"

Write-Output "12 multi-window launcher"
SelectTab "Multi-Window Test"; Start-Sleep -Seconds 5
Shot "12_multiwindow_launcher.png"

Write-Output "13 multi-window tile 4"
Click "Tile 4 Windows (full grid)"; Start-Sleep -Seconds 16
Shot "13_multiwindow_tile4.png"

Write-Output ""
$log = "D:\Bilal\SkiaGrid\SampleApplicationV2\bin\Debug\net8.0-windows\crash.log"
if (Test-Path $log) { Write-Output "=== CRASH LOG ==="; Get-Content $log | Select-Object -First 40 }
else { Write-Output "no crash.log - clean run" }
Get-Process -Id $script:pid0 -ErrorAction SilentlyContinue | Out-Null
if ($?) { Stop-Process -Id $script:pid0 -Force }
Write-Output "DONE"
Get-ChildItem $OUT | Select-Object Name, Length
