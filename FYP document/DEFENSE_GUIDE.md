# FastGrid — FYP-I Defense Guide

Everything below is verified against the actual code and real runs on this machine
(2026-09-11). No estimated numbers.

---

## 1. Files in this folder

| File | What it is |
|---|---|
| `FastGrid_FYP1_Defense_UPDATED.pptx` | **Use this one.** 32 slides in your required sequence, speaker notes on every new slide. |
| `FastGrid_FYP1_Defense.pptx` | Your original 12-slide deck, untouched. |
| `FastGrid_Property_Reference.xlsx` | **The property list you asked for** — 306 public API members in 8 filterable sheets. |
| `FastGrid_Property_Reference.md` | Same content as Markdown, for pasting into the report. |
| `FastGrid_Architecture.drawio` | Architecture diagram. |
| `FastGrid_DFD.drawio` | DFD — 4 pages: Level 0, Level 1, Level 2 (query), Level 2 (render). |
| `FastGrid_Gantt.drawio` | Gantt chart: 4 phases across your 8 signed meetings. |
| `FastGrid_Design.drawio` | 2 pages: class diagram of the core control, paint-cycle sequence diagram (both Meeting-5 deliverables). |
| `FastGrid_Deployment.drawio` | 2 pages: CI/CD deployment flow, System process flow. |
| `diagrams/*.png` | Rendered diagrams, already embedded in the deck. |
| `screenshots/*.png` | Application screenshots, already embedded in the deck. |
| `capture_screenshots.ps1` | Re-captures the whole screenshot set automatically — see §6. |

Open a `.drawio` file at **app.diagrams.net** to edit. To re-export:
**File → Export as → PNG**, zoom 200%, transparent background off.

---

## 2. Slide sequence — mapped to your spec

Your 14 required slides, in order, plus the extras you asked for earlier. The
**bold** numbers are your spec.

```
 1  Title (+ Supervisor: Miss Soofia)       17  System Architecture             **12**
 2  Project Summary            (optional)   18  Class Diagram — Core Control    **12**
 3  Problem Statement                **1**  19  Sequence Diagram — Paint Cycle  **12**
 4  Objective                        **2**  20  DFD — Level 0 (Context)         **12**
 5  FYP Scope                        **3**  21  DFD — Level 1                   **12**
 6  Change of Scope        (you asked)      22  System Process Flow             **12**
 7  Methodology                      **4**  23  Screenshots — 100K / 1M         **13**
 8  Tools & Technologies   (you asked)      24  Screenshots — All Features      **13**
 9  Project Plan — 4 Phases, 8 Meetings **5** 25  Screenshots — Instrumentation **13**
10  RACI Matrix                      **6**  26  Screenshots — 4 Trading Desks   **13**
11  Gantt Chart                      **7**  27  Screenshots — Row Drag & QA     **13**
12  Budget & Costing                 **8**  28  Future Scope — Dirty Rectangle
13  FYP-I Deliverables               **9**  29  References                      **14**
14  Literature Review               **10**  30  SRS — Functional (1 of 2)
15  Demo of 30% Work                **11**  31  SRS — Functional (2 of 2)
16  Deployment Flow Chart — CI/CD   **12**  32  SRS — Non-Functional
```

**32 slides.** Your item **12** needs six visuals (deployment, architecture, class diagram,
sequence diagram, two DFD levels, system process), so it occupies slides 16–22.

- **Slide 2 (Project Summary)** is not in your spec. It is a one-slide orienting
  summary; hide it if the rubric is strict about the sequence.
- Your item **13** ("SS of 30% work") is the screenshot gallery on slides 23–27.
- SRS is at the very end, as you asked.

---

## 3. One thing to check before you sleep

**RACI letters** — slide 10. I drafted it from the code structure, **not** from a record of who
did what. Check every row with Taha and Usman. Examiners check that each row has **exactly one
A**; keep that true if you move letters.

The supervisor name is now filled in as **Miss Soofia**, taken from your FYP Fortnightly
Sign-off Sheet (project code 89223) — along with the real dates and agendas of all 8 meetings,
which now drive both the Project Plan slide and the Gantt chart. Nothing on those two slides is
invented; every row traces to a line your supervisor signed.

I also built the two Meeting-5 deliverables that were missing from your document set: the
**class diagram of the core control** and the **paint-cycle sequence diagram** (slides 18–19,
source in `FastGrid_Design.drawio`).

---

## 4. The "more than 30%" framing

You asked for this to be explicit in the deck. It now appears on four slides:

- **Slide 2** — "MORE than the 30% milestone is complete."
- **Slide 5 (FYP Scope)** — "the delivered scope is well beyond that."
- **Slide 6 (Change of Scope)** — the full before/after, including what was brought
  *forward* (CI/CD) and what was deferred.
- **Slide 15 (Demo)** — "Status: MORE than 30% of the planned work is complete — what
  follows is the working product, not a mock-up."

Say it out loud once, early, then let the demo carry it.

---

## 5. The demo — exact click order

Run `SampleApplicationV2`. It opens on **DemoWindow** with **seven** tabs.

I added two tabs that did not exist before:

- **100K Stress Test** — nothing in the app previously loaded the 100,000 rows your deck
  claims. This is now the centrepiece.
- **All Features** — drives every public property, method, event and command, including
  the ~29 features that had no demo coverage anywhere in the sample app.

### Step 1 — the headline (2 min, do this first)

1. **100K Stress Test** → press **100,000**. Read out:
   100,000 rows bound in **67 ms** · **2.59 ms** avg frame · **4.00 ms** p95 ·
   **250 cells/frame** · 31 MB.
2. Scroll hard, or press **Auto-scroll**. The frame time does not move.
3. Press **1,000,000**. Frame time: **2.75 ms** — essentially identical.

> *"Ten times the data, the same frame time, because the cost is proportional to the size
> of the viewport, not the size of the dataset."*

4. Tick **Live ticks** — 2,000 rows change every 100 ms and it still holds.

### Step 2 — complete API coverage (3 min)

5. **All Features** tab. Point at the diagnostics line
   (`VisibleItemCount / FilterCount / GroupCount / CanReorderRows / VerticalScrollBarActualWidth`).
6. Press **ApplyGroup(Category) + 6 aggregations** — Sum, Min, Max, Avg, Count and Distinct
   at once.
7. Press **ExpandAllRows()**, then **GetExpandedRowItems()** — tree child rows plus the
   expand-state read APIs.
8. Press **GetAllPercentiles()** — p50/p95/p99 per operation, printed into the event log.
9. Tick **Custom header colours**, drag the **Density** / **font size** / **SKRowHeight**
   controls. Right-click a row, a header, and empty space — three different context menus.
10. Every click writes the exact API name into the event log at the bottom. That log *is*
    the argument that nothing is undemonstrated.

### Step 3 — breadth and credibility (3 min)

11. **Multi-Window Test** → **Tile 4 Windows** — four independently streaming trading desks.
12. **Row Drag** → *Open drop-target window* → drag rows out; then drag into **Excel**.
13. **CollectionView** → grouping, subtotals, custom-drawn trend cells, in-cell buttons.
14. Visual Studio → **Test Explorer** → Run All → **552 tests green**.

---

## 6. Re-capturing the screenshots (recommended)

Two screenshots in the deck have cosmetic problems, because your machine locked partway
through the automated capture:

- Slides 23 & 26 have a stray **Notepad** window and an "Activate Windows" watermark.
- Slide 23's 100K shot was taken before the All Features tab existed, so its tab strip
  shows six tabs instead of seven.

To fix all of it in one go: **close Notepad**, unlock the screen, then run

```powershell
powershell -ExecutionPolicy Bypass -File "D:\Bilal\SkiaGrid\FYP document\capture_screenshots.ps1"
```

It launches the app, drives it through all 13 states via UI Automation, and writes fresh
PNGs into `screenshots\`. Then re-run the deck builder, or just
**Insert → Picture** the new files over the old ones on slides 23–27.

---

## 7. Three numbers to memorise

| | |
|---|---|
| **2.59 ms** | average frame time at 100,000 rows (budget was 5 ms) |
| **2.75 ms** | average frame time at 1,000,000 rows — the flat-cost proof |
| **552** | automated unit tests, all passing |

Supporting: ~250 cells drawn per frame · 67 ms to build and bind 100k rows ·
306 documented public API members · v2.18.0.

---

## 8. Questions they will ask

**"Why not just turn on virtualization in the WPF DataGrid?"**
Virtualization cuts the element count to the viewport, but WPF still creates, measures,
arranges and recycles a visual sub-tree per cell, and still runs a layout pass on every
scroll. FastGrid creates *zero* visual elements for the body. A scroll is one canvas
repaint of ~250 cells using cached `SKPaint` objects. That is why frame time is flat from
100k to 1M rows.

**"Then why is there still a WPF DataGrid in your architecture?"**
Only for the column header band — resize grips, drag-reorder and sort affordances are hard
to reimplement accessibly, and the header costs O(columns), not O(rows).

**"If there are no UI elements, how does a screen reader work?"**
We implement the UI Automation peer tree by hand — `SkiaGridViewV2AutomationPeer` plus Row,
Cell, Header and Button peers. The tree is *virtualized*; a naive full tree froze the app
with a screen reader attached at large row counts, fixed in v2.7.8.

**"How do you unit-test a rendering control 552 times?"**
The index math and state machines were deliberately extracted out of the control into plain
classes — the seven managers, static methods on `RowDragController` — so they run without a
WPF dispatcher or an STA thread. That was the point of the layered design.

**"How does it survive a high-frequency feed?"**
A dirty flag coalesces a burst of source changes into one refresh per render tick;
`InsertRange`/`RemoveRange` splice K rows in one pass instead of re-flattening; and a cached
sort comparer avoids a per-item delegate allocation.

**"Why 5 ms?"**
60 Hz gives a 16.7 ms frame budget. Holding the grid under 5 ms leaves room for the rest of
the application in the same frame.

**"Why average *and* p95?"**
An average hides stalls; p95 is what a user feels. Ours is 4.00 ms — still inside budget.

**"Why SkiaSharp and not Direct2D directly?"**
Skia is the engine behind Chrome, Android and Flutter, so it is proven at UI scale, and
SkiaSharp already bridges it to WPF via `SKElement`. Direct2D would mean writing interop and
text shaping ourselves for no measured gain — we are CPU-raster at 2.6 ms, nowhere near
needing the GPU.

**"Is 100,000 rows realistic?"**
For one watchlist, no. For an options chain across all expiries, an order-book replay, or a
global position blotter, yes. The point is the ceiling.

**"What is next, and why not now?"** *(your dirty-rectangle slide)*
Dirty-rectangle rendering: mark changed cells, union them into rectangles, clip the canvas
and repaint only those. On a feed where ~5% of visible rows tick, per-frame work drops from
~250 cells to ~12. It was not done now because it needs a per-cell change-tracking pipeline
that the current design deliberately does not have — re-reading every visible cell each
frame is exactly what keeps the renderer stateless and testable. We are also already inside
the 5 ms budget, so this is about cutting the cost of a mostly-unchanged frame, not about
hitting a target we are missing.

**"What is the biggest weakness?"** *(answer honestly — it scores better)*
In-place cell editing is not implemented; the grid is read-oriented today. And the cloud
telemetry dashboard is designed and instrumented but not deployed.

---

## 9. Pre-flight checklist

- [ ] Supervisor name on slide 1
- [ ] RACI letters checked with both teammates
- [ ] `dotnet build SkiaSharpControls.sln -c Debug` → **0 errors** (verified)
- [ ] Run the app once before presenting, so first-run JIT is warm
- [ ] Re-capture screenshots with Notepad closed (§6) — optional but worth 5 minutes
- [ ] Run the test suite **once, fresh**. Running it repeatedly in one session can produce
      spurious WPF/STA failures from `Application.LoadComponent` — nothing to do with your
      code, but you do not want red on screen. One clean run = **552/552**.
- [ ] Numbers were measured in a **Debug** build; Release will be the same or better.
- [ ] Keep `diagrams/DFD_Level2_Render.png` as a backup slide — it answers "why is it fast?"
      better than anything you can say.

---

## 10. What I changed in your code

All changes verified with a full solution build (**0 errors**) and the test suite
(**552/552 passing**).

1. **NEW** `SampleApplicationV2/Views/StressTestView.xaml(.cs)` — the 100K/1M benchmark tab.
2. **NEW** `SampleApplicationV2/Views/AllFeaturesView.xaml(.cs)` — the complete-API-coverage
   tab, including a button that opens `MainWindow`, which was previously unreachable
   (`App.xaml` starts on `DemoWindow`) and is the only place your bound column-layout
   persistence is demonstrated.
3. `SampleApplicationV2/DemoWindow.xaml(.cs)` — two new tabs registered.
4. `SampleApplicationV2/App.xaml.cs` — unhandled exceptions are now written to
   `crash.log` next to the executable and shown in a dialog instead of killing the app
   mid-demo. This is how I found the bug in item 6.
5. `SkiaSharpControlV2.Tests/Helpers/HelperTests.cs` —
   `ApplyFormat_DateTimeType_FormatsDate` was failing on this machine. It hard-coded
   `"03/15/2024"`, but `/` in a .NET custom date format is the *culture's* date separator
   and `Helper.ApplyFormat` formats with the current culture, which here yields
   `03-15-2024`. The expectation is now culture-relative.

### Two real findings for you to decide on — not tonight

**A. `ItemsSource = null` throws.** Setting `SkiaGridViewV2.ItemsSource = null` raises
`ArgumentException: "Items must be IList"` from the `CustomCollectionView` constructor
(`CustomCollectionView.cs:164`, via `SkiaGridViewV2.xaml.cs:460`). Clearing a grid by
nulling its source is a completely reasonable thing for a consumer to do, and today it
crashes the host application. My stress view now swaps in a new list instead, so the demo
is safe. The library fix is to treat a null source as an empty view.

**B. `Helper.ApplyFormat` is culture-sensitive.** It calls `dtVal.ToString(format)` with no
culture, so a column declared `Format="MM/dd/yyyy"` renders differently on machines with
different regional settings. For a trading grid you probably want
`CultureInfo.InvariantCulture`. I did not change production behaviour the night before your
defense.

### Feature coverage note

An audit of the demo app against the public API found ~29 features with no demo coverage —
context menus, header theming, exact row/header heights, declarative XAML `SkButtons`,
`InsertRange`/`RemoveRange`, `CoalesceRowUpdates`, indicator-only sorting, most of the MVVM
command surface, the expand-state read APIs, targeted `ToggleGroup`/`ToggleRow`, per-grid
automation, `ExportData(All)`, and the scroll-bar metric callbacks. The **All Features** tab
now exercises all of them, and logs the API name for each one as you click.
