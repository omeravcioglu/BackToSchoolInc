# Back To School Inc.: boarding school management sim

A 2D school tycoon and boarding school simulator. You start with a tiny, struggling school and turn it into a
prestigious institution. This repository contains the **simulation core** (all game rules, as pure C#), a
**2D tilemap campus** drawn in the "blue ballpoint on graph paper" style with the school's **students and staff
walking around it**, the **notebook UI** (UI Toolkit) from the design canvas (title menu, new school, assistant pick,
HUD, tutorial, build palette and a notebook page for every part of the school), **music and sounds** made in code,
saving and loading, and a **debug UI** (IMGUI) kept behind `F1` in development builds.

## Play it

1. Open the project in Unity 6000.0.32f1 and press **Play** in any scene. `Bootstrap` spawns the game automatically.
2. On the title screen choose **New school**: name it, pick an emblem and colour, then pick an **assistant** from
   four candidates (personality, experience, salary). Leave "show me around" on for the tutorial.

Time never stops: **one school day every 4 seconds**, with no pause and no fast-forward (two minutes a month,
24 minutes a school year). `Esc` opens the menu, but the school keeps running underneath; only **Main menu** stops the
clock (and **Continue** on the title screen picks up where you left). The day dial in the top bar shows the day going by.

Saving is automatic: a school saves itself at the start of every month, when you leave for the main menu, when it
closes and when the game quits (a small "saved" stamp shows on the clock). There are three slots: **Load** on the title
screen shows them as photos of each campus. Pick one and press Load (or click it twice), delete one, or click an empty
slot to found a new school there. A school that closed stays as a keepsake but can't be played on, and when all three
slots are full the New school sheet says which school a new one will replace. Saves live in
`%USERPROFILE%\AppData\LocalLow\McHunter Studio\Back To School Inc.\Saves` (`Application.persistentDataPath`, from the
company and product names in Player Settings, so those must not change after release).

HUD: the divider tabs on the left open each page (Inbox, News, Students, Staff, Build, Money, Clubs, Stats, Ranks,
Rules, and More for admissions, rooms, administration, the school report, year reports and the school log). A new
school starts with just the Inbox; the tutorial and the school's level bring in the rest (tabs that just appeared wear
a "new!" sticker). `Esc` or the X closes a page. In development builds, `F1` switches between the notebook HUD and
the plain debug UI.

Campus: pan with `WASD`, the arrow keys or right/middle-drag · scroll or `+`/`-` to zoom · click a room for its card
(upgrade, repair, demolish) · click a student or staff member for theirs (open their file, or **Follow** them with
the camera) · `B` build mode (`R` rotates, Shift+click keeps building, `Esc` or right-click leaves) · `L` buy land.

## Layout

```
Assets/Scripts/Core/     SchoolSim.Core: pure C#, no UnityEngine references (noEngineReferences)
  Simulation.cs            Facade: owns state + systems, runs the daily tick
  Simulation.Commands.cs   Every player action (the UI never mutates state directly)
  NewGame.cs               Starting scenario (NewGameSetup: name, crest, seed, assistant, tutorial)
  Save/                    Save files: SaveGame (gzip text, a summary line for the Load screen, then the school as
                           JSON), SaveSerializer (reflection, shared references kept, ISaveHooks rebuilds lookups)
  Model/                   Plain data: GameState, Student, StaffMember, Room, Finance, Inbox, Programs
  Data/                    Tuning and catalogs: Balance, RoomCatalog, TraitRules, SpecializationRules, Names
  Systems/                 One class per system (see below)
  Events/                  48 inbox event templates (parents, students, staff, facilities, external, and the
                           assistant's problems, the tutorial's first decision and press inquiries)
Assets/Scripts/Game/     SchoolSim.Game: Unity layer
  GameController.cs        Phases (title / playing), the clock (one day every 4 s, no pause, no fast-forward) and
                           autosave
  Save/SaveSlots.cs        The three save slots: written on a worker through a temp file, previous one kept as .bak
  GameSettings.cs          Player preferences: full screen, notebook size, assistant tips, music and sound volumes
  Audio/GameAudio.cs       Plays the music and sounds from Assets/Audio/Resources (see "Music and sounds")
  GameClock.cs             The clock everything on screen moves by: real time, or exactly 1/60 s a frame while the
                           trailer records
  ReleaseChecks.cs         "-playtest", "-perftest", "-shots", "-keyart" and "-trailer" player flags (see
                           "Releasing")
  Bootstrap.cs             Auto-spawns the controller, campus, people, camera and UI on Play
  Campus/                  The 2D campus (see below)
  People/                  Students and staff on the map (see below)
  Hud/                     Notebook HUD (see below)
  DebugUI/                 IMGUI management UI (Campus + 12 tabs), shown instead of the HUD with F1 in development
                           builds
  UiTour.cs                "-uitour <folder>" player flag: screenshots the title, new-school and assistant screens,
                           the tutorial, then plays 4 months, grows the campus (land, outdoor spaces, a sports
                           complex, decorations) and shots the people, a student's and a staff member's cards, the
                           build palette, land, room cards, every notebook page, the menus, settings, a level-up
                           and the plain debug view, and quits. Add "-gallery" to build every room at its lowest
                           and highest level instead. Saves go to <folder>/saves; the tour also checks that the
                           school reads back from disk unchanged and shoots the Load screen and a loaded school.
  UiAudit.cs               "-uiaudit <folder>" player flag: the states the tour skips. Every page of a brand-new
                           school, then a busy one with every tab and filter pressed (buttons are pressed in code
                           through GameHud.TourPress, so no mouse or window focus is needed), room cards of every
                           kind including a building site, this year's draft report, and a school closing. Writes
                           audit.txt listing anything it couldn't press or set up.
Assets/Scripts/Editor/   Tools > School Sim > Build Windows Player (development), Build Windows Release, Make App
                         Icon, Rebuild HUD Panel Settings
Assets/Art/              AppIcon.png, drawn by Make App Icon from the game's own crest
Assets/Audio/Resources/  Music/ (Title.ogg, Campus.ogg) and Sfx/ (one .wav per sound), made by Tools/Music
Assets/UI/Resources/     Hud/ (Hud.uxml, Hud.uss, Screens.uss, HudTheme.tss, HudPanelSettings.asset, and Pages/
                         with one stylesheet per page, loaded automatically) and Fonts/ (Gochi Hand, Playpen Sans,
                         Caveat, with their OFL licenses)
Assets/Tests/EditMode/   NUnit tests for the core (Window > General > Test Runner)
Tools/SimHarness/        Headless balance harness (outside Assets, not part of the Unity build)
Tools/Music/             The music and sounds as scores (songs.py, sfx.py) and render.py, which records them
Tools/Trailer/           make_trailer.py: records the trailer with the release build and cuts it to its music
Tools/StoreArt/          make_store_art.py: Steam capsules and screenshots from captures of the release build
Marketing/Steam/         Store page draft (store-page.md) and its images
```

## How the simulation works

One tick = one day. The calendar runs 12 months of 30 days: **Sep–Jun is the school year**, **Jul–Aug is summer and Admissions Season**.

Daily order: facilities (construction, dirt, wear) → staff (energy, morale) → derived indices → students
(happiness, stress, grades, incidents, parents) → relationships → sports and clubs → school stats and reputation →
money → promises → inspections → scheduled consequences → random events → admissions → inbox expiry → administrators.

Everything feeds everything else through **explainable breakdowns**: every stat stores the terms that produced it,
and the Overview tab shows them ("Why is Parent Satisfaction 39? −6 Broken promise, +5.1 Value for money...").

Example chain, emergent rather than scripted:
cheap food → student happiness target drops → families' satisfaction target drops → food complaints arrive in the
inbox → you promise better food → you don't deliver → **broken promise**: trust falls → overall prestige falls →
fewer applicants accept offers and fewer families re-enroll → less tuition.

| System | What it owns |
|---|---|
| `StatSystem` | Daily indices (seats, beds, staff coverage, lounges, places to play, gardens, picnic spots, campus beauty...), the 10 school stats, 7 reputation categories drifting toward targets |
| `StudentSystem` | Per-student happiness, stress, grades, ability growth, behavior, incidents (fights, bullying, parties, cheating, vandalism), homesickness of students from abroad, parent satisfaction, re-enrollment odds |
| `RelationshipSystem` | Pairwise scores from roommates, classmates, teammates and clubs; trait compatibility; friends, best friends, rivals, enemies, crushes (with hysteresis) |
| `StaffSystem` | Energy and workload, morale, market salaries (rise with prestige), raise requests, rival poaching, resignations, 1/3/5-year contracts, severance, training |
| `FacilitySystem` | 39 room types (rooms, sports grounds, outdoor spaces, decorations, each specialization's signature building, the International House and the old manor) with up to 5 levels, placement on the campus grid, construction time, cleanliness (custodians), condition (maintenance), land parcels and the old estate, bed assignment |
| `EconomySystem` | Monthly billing (with the international fee and the signature building's takings), ledgers, projections, tuition changes at semester start with a consequence estimate, loans, the endowment, 120-day insolvency grace |
| `InboxSystem` / `EventDirector` | Message delivery, replies, expiry (ignoring has consequences), random and directed events, cooldowns |
| `PromiseSystem` | Commitments made in replies, checked daily; kept builds trust, broken costs trust and mood |
| `AdministrationSystem` | Directors auto-answer messages in their area using your policy thresholds, with skill-based mistakes |
| `AdmissionsSystem` | Admissions Season pool, weekly transfer applicants, families from abroad, national scholars, offers that families can decline, aid and scholarships, marketing |
| `InspectionSystem` | Five-day notice, eight scored categories, fines, follow-ups, room closures |
| `SportsSystem` / `ClubSystem` | Teams, coaches, intensity, injuries, a season against rivals, finals, ticket income; clubs, skill growth, competitions |
| `RivalSystem` | Eight AI schools with trends; national rankings; poaching source |
| `ProgressionSystem` | School level unlocks (clubs 3, admins 4, scholarships 5, advanced dorms 7, specialization 8, sports 10, students from abroad 12, endowment 16, the old estate 25, elite institution 30...), rooms by level (library 2, gaming room 3, gym and computer lab 4...), director level (at level 3 you can see hidden consequences), and what the director can see (`Reveal`: panels that appear through the tutorial, level 2 or events) |
| `YearCycleSystem` | Graduation and alumni outcomes, re-enrollment, grade advance, annual report |
| `AssistantSystem` | The director's assistant: every 3 days looks for trouble (money, beds, bathrooms, seats, teachers, safety, food, dirt, repairs, moods, upgrade ideas) and brings it to the inbox with ready fixes; recommends an option on every decision; their personality's perk and quirk |
| `NewsSystem` | The papers: big moments (inspections, finals, rankings, level-ups, graduations, incidents, broken promises, money trouble, fee changes, new buildings), weekly features about very good or bad conditions, monthly rival stories. Stories about the school nudge its reputation; bad ones can bring a reporter's call |
| `HistorySystem` | A daily snapshot of the school (money, students, prestige, rank, every stat, rivals' scores) for the statistics and rankings pages |
| `TutorialSystem` | The assistant's 10-step walkthrough; each step reveals one part of the game and waits for the director to use it. Random trouble holds off for the first 20 days |

## The assistant, the tutorial and progression

When you found a school you hire an assistant from four candidates with different personalities:

| Style | Perk | Quirk |
|---|---|---|
| Sunny | Staff morale +4, student life +5 | Slow to spot money trouble |
| Stern | Safety +6 | Students a bit tense (-2 mood) |
| Thrifty | Upkeep -10%, better pay deals | Staff morale -2 |
| Charming | Good press (bad press softened), parents +3 | Costs 35% more |
| Bookish | Education +4, training -20% | Misses dirt and repairs |
| Handy | Repairs -25%, rooms wear 20% slower | Blunt with parents (-1) |

Skill (1-5 stars) decides how early they notice trouble and how often their advice is right; better assistants cost
more. They are paid with the payroll. Their problem messages ("We're out of beds") come with fixes that work straight
from the inbox (build a dorm, buy land, hire the best candidate, book a deep clean, switch the menu, throw a party...),
always with a "Not now" that makes them wait twice as long before asking again. Every other decision gets their
recommendation, marked in the inbox.

The tutorial (skippable) is told by the assistant in a speech bubble with an arrow pointing at the next thing to
click: inbox, a first practice decision, building a dorm, a room's details, students, money, vitals and the staff
room. A new school starts with only the inbox visible; steps reveal the rest, and level 2 brings rules, land and
admissions. Rankings appear with the first monthly league table and the News tab with the first story. Level-ups get a
card listing what just unlocked (features, panels and rooms).

## The campus

The map is 100x50 tiles, split into 10x10 **land parcels**: 70x50 for the school and, across the road to the east,
the walled **old estate** (see [The late game](#the-late-game)). The school starts owning a 2x2 block in the middle;
parcels next to owned land can be bought, each costing 35% more than the last. Rooms have fixed footprints in tiles
(a dorm is 4x3, a cafeteria 8x5, a soccer field 12x8) and are placed with a **rotation** that names the side their
door faces. Placement rules live in the core (`FacilitySystem.CheckPlacement`), so the UI, auto-placement, the
harness and the tests all share them:

- the footprint must be inside owned land and must not overlap another room;
- the tile in front of the door must be free and owned;
- a room may not cover the tile in front of another room's door.

`CampusMap` (in `GameState`) holds parcel ownership, a tile-to-room occupancy index, door-front marks and a
`Version` counter that the view watches to redraw.

Besides rooms there are **outdoor spaces** (playground, garden, practice field, picnic area: fenced, with a gate),
the **sports complex** (school level 8: a 12x9 hall with a pool, a court and a running lane, home ground for
basketball, swimming, track and tennis) and **decorations** (trees, flower beds, benches, a fountain at level 3, the
founder's statue at level 5). Decorations have no door, are ready the day they're placed, only wear slowly and don't
count toward room condition or cleanliness. Together they make the campus prettier: a beauty score lifts student
life, families' mood and admissions interest; the playground cheers up grades 1 and 2, and gardens ease stress.

Rendering (`Assets/Scripts/Game/Campus/`):

| File | Role |
|---|---|
| `InkCanvas.cs` | Palette tokens from the design canvas, plus a tiny software rasterizer: signed-distance shapes with anti-aliasing and a noise "wobble" so lines look hand-drawn |
| `CampusArt.cs` | Generates and caches every tile and sprite at runtime: graph paper, hatched lawn, dashed land edges, floor patterns per room type, blueprint grid, ghost outlines, highlight, trees, sign cards |
| `RoomPainter.cs` | Draws each room type's walls, door swing and furniture. Furniture follows level and capacity: a level-1 dorm shows three bunk beds for six boarders, a level-4 dorm two beds and a private bathroom |
| `CampusView.cs` | Tilemaps (ground, land edges, floors, heat map, build grid, ghost, land) and sprites (rooms, shadows, labels, construction and upgrade signs, trees, selection) |
| `CampusController.cs` | Tools: select rooms and people, build (ghost + validation + placement), buy land. Everything goes through `Simulation` commands |
| `CampusCamera.cs` | Orthographic pan and zoom with the Input System, and following someone around |

No image assets are needed: art is generated when first used and cached per room type, level and rotation. The
labels use the design's handwriting font, `Caveat-Bold.ttf` from `Assets/UI/Resources/Fonts` (any `Resources/Fonts`
folder works; without it they fall back to Unity's built-in font).

### People on the map

`Assets/Scripts/Game/People/` puts the school's real students and staff on the campus (up to 64 students and 18 staff;
most students go home for the summer, and sick ones stay out of sight). It's purely visual: the simulation never
reads it.

- **Looks** come from the person: skin, hair style and colour, glasses, freckles, bows and headbands are picked from
  their id, students wear their grade's colour, staff dress for the job (a chef's hat, a nurse's cap, a hard hat
  for maintenance, a coach's whistle, a teacher's tie, a security badge), and faces follow their mood. The HUD's
  portraits use the same drawings.
- **Walking**: an A* path over free owned land (8 directions, no corner cutting, smoothed), in through the door or
  gate and to a spot inside. The grid is rebuilt whenever the map changes.
- **What they do** depends on what's built and who they are: class, meals and picnics, their own dorm, the library
  (bookworms), fields and courts (athletes, and players near their team's ground), the playground (grades 1 and 2),
  gardens (anxious and introverted kids), lounges, benches. Teachers go to classrooms, chefs to the cafeteria,
  custodians to the dirtiest rooms, maintenance to the most worn, security patrols.
- **Life**: two walking frames with a bob, sitting in classrooms, cafeterias, libraries and lounges, running at play,
  a ball passed around when two or more are playing, and thought bubbles (a book, an apple, a heart, a sweat drop, a
  cloud, zzz...) for what they're doing or how they feel.
- **Picking**: click someone for their card (polaroid, what they're doing, chips, four bars, their thought, "Open
  file" and "Follow"). People sort in front of each other and of decorations by how far down the screen they stand.

| File | Role |
|---|---|
| `PersonLook.cs` | Stable looks from a person's id, outfits by grade and role, the expression for their mood |
| `PeopleArt.cs` | Portraits, heads and full figures (standing, two walking frames, sitting) as vector drawings |
| `PeopleSprites.cs` | Rasterizes figures, thought bubbles, the shadow, the selection ring and the ball into cached sprites |
| `CampusPeople.cs` | Spawning, the walkable grid and A*, choosing activities, animation, bubbles, balls, picking |

## The late game

Each unlock from level 8 on brings something big to build or spend money on. Most of it pays something back, but
slowly, so money keeps mattering:

- **Signature buildings** (level 8, once the specialization is chosen): each identity has one big building of its own,
  three levels, dear to run, shown in the build palette only to that school. The Academic Academy's **Scholars' Hall**
  (teaching, academic reputation, better universities for graduates, a summer school), the Sports Academy's **Arena**
  (stronger teams, fewer injuries, home ground for basketball, tennis and track, event nights), the Luxury Boarding
  School's **Clubhouse** (dorms, food, families, higher dorm fees), the Arts Academy's **Theatre** (student life, arts
  clubs, shows all year), the Technology School's **Innovation Lab** (teaching, tech clubs, industry partners) and the
  Community School's **Community Centre** (trust, safety, more families applying, a council grant). What each does is
  a table in `SpecializationRules`; the building's level, cleanliness and repair scale it.
- **Students from abroad** (level 12): families from abroad join the admissions pool and the transfers. They pay 40% on
  top of tuition and need a bed like everyone, but start homesick (unhappy, stressed, slower in class, their families
  worried). It fades over a few months, faster with friends and language teachers and much faster with an
  **International House**, which also draws more of them. A globe marks them on the Students page; their admission
  cards show the country.
- **The endowment** (level 16, on the Money page): money given to a fund for good. Each September it pays out 3% and
  grows 2%, and it lends prestige: the bigger the fund the more, ever more slowly.
- **The old estate** (level 25): a walled park with a boarded-up manor, across the road. Buying it ($800K, from the land
  map or the trustees' letter) brings the manor and its drive; the rest of the estate is then for sale parcel by
  parcel, from $150K and rising more gently than the main campus. The manor is a listed building that can be restored
  twice for prestige, student life and happier families. People cross the road to get there.
- **Elite institution** (level 30): more prestige, national scholars in the admissions pool, an endowment that counts
  for more, and a Founders' Gala every March that raises money (or adds it to the endowment).

## The HUD

`Assets/Scripts/Game/Hud/` builds every screen from the design canvas with UI Toolkit: the title menu (with its
hand-drawn school backdrop), new school, assistant pick, settings, the in-game menu, level-up and game-over cards; in
game the top bar (school crest with level and next unlock, money and monthly result, students and beds, prestige and
rank, mood and trust with their weekly change, calendar page and day dial), the divider tabs, the inbox clipboard, the
promises sticky, the school vitals with weekly trends, toasts, the assistant's portrait and speech bubble; on the
campus the **build palette** (categories, cards with the real room drawings, details, auto-place), **buying land** on
a parcel map, the selected **room's card** (meters, residents, next level, upgrade, repair, demolish) and the selected
**person's card**; and a notebook page for everything else:

- **Inbox**: a clipboard of letters (decisions first), the open letter with who's involved, reply cards with costs,
  hints and the assistant's pick, and what happened after you answered.
- **Students** and **Staff room**: rosters with faces, then a file per person (bars with their "why?", traits,
  abilities, family, friends, diary, moving rooms; pay, contract, training, morale, hiring and letting go).
- **Money** (the bank, money in and out, balance chart, tuition with its consequences, loans and sponsors),
  **School rules** (food, curfew, discipline, internet, ticket prices) and **Administration** (directors' desks and
  their standing orders).
- **Admissions** (applicants, offers and aid, the campaign and the season calendar), **Clubs & teams**, **School
  report** (every stat with a grade and its "why", reputation, what needs attention, promises), **Year reports**,
  the **School log** (a diary by month) and **Rooms** (every room as a card; click one to fly to it).
- **News** (front page and clippings), **Statistics** (14 metrics with sparklines, a chart by month, year or all time
  and a "why?" breakdown) and **Rankings** (overall, academic, sports and size league tables, rank history, what lifts
  the score).

| File | Role |
|---|---|
| `GameHud.cs` | Builds the UIDocument, phases (title or playing), navigation, the More menu, the clock, unlock-driven tabs, and decides when the map takes clicks |
| `GameHud.PageHooks.cs` | The page contract: each page in `Pages/` fills in `Build…Page`, `Refresh…Page` and `Reset…Page` partial methods; also `ShowPageAt` (open a page on a given letter or person) and `RunCommand` (run a sim command, toast the result) |
| `Pages/` | One page each (some split in two or three files), named with their own prefix (`ibx`, `stu`, `stf`, `mon`, `rul`, `adm`, `adx`, `clb`, `rpt`, `yrp`, `log`, `rms`) because USS classes are global; their stylesheets live in `Assets/UI/Resources/Hud/Pages/` |
| `GameHud.Campus.cs` | Build mode header and palette, buying land, the selected room's card |
| `GameHud.PersonCard.cs` | The selected student's or staff member's card and the thought bubble over their head |
| `GameHud.Menus.cs` | Title, new school, assistant pick, settings, in-game menu (the clock keeps running), level-up and game-over screens |
| `GameHud.Assistant.cs` | The assistant's portrait, speech bubble, tutorial arrow and "new!" stickers |
| `GameHud.Pages.cs` | News, Statistics and Rankings pages |
| `GameHud.Widgets.cs` | Live panels: top bar numbers, inbox rows, promises, vitals, trends (a week of daily snapshots), toasts. The right column drops rows rather than run off small screens |
| `InkElements.cs` | Custom elements USS can't draw: `InkBox` (paper fill, ink outline, offset ink shadow, wobbly elliptical corners, dotted or ruled paper, styled with `--ink-*` USS properties), `InkIcon`, `Tape` (washi tape), `DashLine` |
| `PersonPortrait.cs` | A student's or staff member's face, the same drawing as their figure on the map |
| `InkWidgets.cs` | `Portrait` (assistant faces), `CrestView`, `DayDial`, `LineChart`, `Starburst` |
| `InkDrawing.cs`, `Illustrations.cs` | Hand-drawn pictures as SVG-style shapes with dashes and pencil hatching: the title backdrop, the kids, level-up confetti |
| `InkIcons.cs`, `InkPath.cs` | The design's icons as SVG path data, drawn with Painter2D through a small SVG path reader, so no image files are needed |

Layout and look live in `Assets/UI/Resources/Hud/Hud.uxml`, `Hud.uss`, `Screens.uss` and `Pages/*.uss` (1440x810
reference, "Expand" scaling; the Size setting switches to 1600x900 to fit more on screen). The panel has no default
theme, so `Screens.uss` also lays out ScrollView's parts (clipping, natural-height content, a thin inked scrollbar).
Avoid Gochi Hand for words with a capital G or a lowercase z: they read as 6 and 3.

## Music and sounds

Everything you hear is played by one small band of real sampled instruments: vibraphone, piano, upright bass, brushes,
clarinet, pizzicato strings, marimba, flute, celesta, harp and bassoon. They come from the GeneralUser GS soundfont by
S. Christian Collins, whose licence allows its use in commercial music. The music and sounds are written as notes in
`Tools/Music` and recorded by FluidSynth into audio files. The game ships only those files, never the soundfont.

- `songs.py` holds the tunes. The campus plays a cozy swing theme in F at 104 bpm (A A B A C A, 48 bars, 111 s). The
  title screens play the same tune slowly on piano and strings (80 bpm, 48 s). The trailer has its own cut of the
  theme, described under "Releasing". Melodies are written by hand and stay between E4 and G5, so nothing whistles.
  Bass lines, piano comping, pizzicato and string pads are built from chord symbols with smooth voice leading. Every
  accompaniment chord stays under the tune and avoids notes a semitone from it. A last pass removes anything left
  sounding a minor second or ninth against another note.
- `sfx.py` holds the sound effects, on the same instruments and between C3 and G5:
  - a marimba tock for buttons and a vibraphone note for selecting;
  - a harp flourish for pages and a vibraphone third for notes;
  - a celesta ding-dong for letters and a marimba arpeggio for coins;
  - a marimba and tom thunk for building and a tumbling pizzicato for demolishing;
  - a bassoon uh-oh for mistakes, two vibraphone notes for the assistant, and a vibraphone, celesta and piano
    flourish for level-ups.
- `render.py` writes each score as MIDI, plays it through FluidSynth with the soundfont, and masters it:
  - It folds the loops' reverb tails back onto their starts, so they repeat without a seam.
  - A gentle master EQ goes on: rumble out, a little less 450 Hz, a soft lift above 4.5 kHz.
  - One constant gain brings each piece to its loudness (EBU R128): -18 LUFS for the game's music, -16 for the trailer.
  - Each sound gets its own peak, quiet for the clicks you make all the time and louder for news and celebrations.
  - It prints each piece's loudness and where its energy sits by octave. `--stems` measures each instrument alone.

```
python Tools/Music/render.py            (everything; or campus, title, trailer, sfx)
```

It needs Python with numpy, ffmpeg on the PATH and, in `Tools/Music/vendor` (ignored by git):
- `GeneralUser-GS.sf2` from github.com/mrbumpy409/GeneralUser-GS;
- FluidSynth 2.6.1 (`fluidsynth-v2.6.1-win10-x64-cpp11.zip`, unzipped into `vendor/fluidsynth`) from
  github.com/FluidSynth/fluidsynth/releases.

`AudioImport` (an editor import rule) makes the music stream and the sounds load ready to play. `GameAudio` plays them:
- It cross-fades between the title and campus tunes.
- It listens to the game for letters, level-ups, building, buying land and selections.
- It takes clicks, page turns, notes and the assistant from the HUD.

Music and Sounds have ten-step volume controls in Settings.

## Tuning

Almost every number a designer would touch is in `Data/Balance.cs`, `Data/RoomCatalog.cs` and
`Data/SpecializationRules.cs`. After changing them, run the harness:

```
cd Tools/SimHarness
dotnet run -- 8 12345 smart
```

It prints a monthly dashboard, a ledger breakdown and an annual report for every year. Modes: `smart` (competent
scripted director), `rich`, `random`, `first`, `passive`. Flags: `-assistant` (or `-assistant=Thrifty`) hires an
assistant, whose problem fixes the smart director accepts; `-news` prints every story; `-nogating` makes every room
buildable from the start; `-savetest` saves and reloads the school at the end and checks that the copy plays on
exactly like the original for 90 days. The smart director picks its specialization by seed (so every signature
building gets played), builds it, adds an International House, buys the old estate and restores the manor, and gives
whatever is left beyond a few months' running costs to the endowment. With the current numbers it ends year 8 with
roughly 115–220 students, ranked #3–#6, and $0.2–1.3M in the bank on the seeds tested (with or without an assistant);
random and passive play go bankrupt.

## Releasing

**Build**: Tools > School Sim > Build Windows Release (or, from a terminal,
`Unity.exe -batchmode -quit -projectPath <project> -executeMethod SchoolSim.EditorTools.BuildScript.BuildWindowsRelease -buildPath <folder>`).
A release build has no "Development Build" mark and no debug view (F1 and the More menu's "Plain debug view" exist
only in development builds), and gets a `Licenses` folder next to the exe with the credits and the fonts' SIL Open Font
License texts (the in-game credits are behind the link at the bottom of Settings). The version (Player Settings,
1.0.0) shows there too. The icon is `Assets/Art/AppIcon.png`; Tools > School Sim > Make App Icon redraws it.

**Checks**: run the built player with one of these flags (plus `-screen-width 1440 -screen-height 810
-screen-fullscreen 0`); each writes `report.txt` and screenshots to its folder and quits. Leave the playtest's window
in front: Unity drops UI clicks while its window is in the background, so each click waits up to 15 seconds for focus
to come back, and the report says if focus was lost.

| Flag | What it does |
|---|---|
| `-playtest <folder>` | Plays the main actions with a virtual mouse and keyboard on the real UI: founds a school through the menus, answers the tutorial letter, skips the tutorial, levels up, builds a dorm on the map, buys land, hires someone, plans a tuition change, picks the food, clicks and follows a student, turns the music up, goes to the title and back. Each step is checked against the simulation and marked PASS or FAIL |
| `-perftest <folder>` | Grows a school for two years, then measures frames in real time at three zoom levels and with a page open (average, median, 95th and 99th percentile, worst, slow frames, garbage collections) and how long a school day takes to simulate |
| `-shots <folder>` | A quick set of screenshots (title, campus, a person's card, build mode, six pages, settings) to check other screen sizes and aspect ratios. `-big` grows a bigger school with outdoor spaces and decorations first; `-supersize N` captures at N times the window size |
| `-keyart <folder>` | The title art without its menu, with and without the logo, for store images (use `-supersize 3`) |
| `-trailer <folder>` | Records the trailer's shots at 1920x1080 and 60 fps on a fixed clock: every frame is exactly 1/60 s of game time however long it takes to capture, so the video is perfectly smooth. Each shot's length comes from `<folder>/cuts.txt`, the bars of the trailer music, so every cut lands on a beat. Use `make_trailer.py` rather than calling it directly (below). Add `-still` to hold the camera and save PNG frames, for the store description's GIFs |

**Trailer**: run `python Tools/Trailer/make_trailer.py record <BackToSchoolInc.exe> <capture>`, then
`python Tools/Trailer/make_trailer.py build <capture> Marketing/Steam/trailer.mp4`.
- `record` renders the trailer music, writes the cut list and records the tour. Don't minimise the window.
- `build` adds the caption cards, a paper-white flash on each cut and the music, then encodes H.264 1080p60.
- The music is the campus theme cut to the shots, 31 bars at 112 bpm:
  - a piano intro over the title;
  - the tune twice as the school comes to life;
  - quick one-bar pages;
  - a lift on the level-up card into a key change (F to G) over the whole campus;
  - a slowing cadence that rings out on the end card.

**Store page**: `Marketing/Steam/store-page.md` has the descriptions, features, tags and a system requirements draft.
The images come from the release build: capture with `-keyart <captures> -supersize 3` and
`-shots <captures>/shots -big -supersize 2`, then run `python Tools/StoreArt/make_store_art.py <captures> Marketing/Steam`
(it needs Pillow). It crops the title art into every Steam capsule size, draws the logo with Gochi Hand where a crop
needs it, makes the transparent library logo and scales the screenshots to 1920x1080.

**Version control**: the project is a git repository with a Unity `.gitignore` (Library, Temp, Logs, UserSettings and
builds stay out) and `.gitattributes` (LF line endings, binary fonts and images).

## Not built yet

- **Paths and corridors**: people cut across any free land; there are no paths to lay down yet, and rooms don't
  connect inside. People don't block or avoid each other.
- **Moving rooms** and variable-size rooms; furniture is drawn from level and capacity rather than placed by hand.
