# The Dialogue File Format

This is a guide for writing `.dialogue` files. It's written for the people writing the actual
conversations and events - you don't need to know how to program, although dialogues can call into
the game and small bits of "code" will show up in a few places. Everything you need is explained
here, with examples you can copy.

A `.dialogue` file is a plain text file. You can edit it in any text editor; there's a VS Code
extension in `Extras/SyntaxHighlighter` that colors everything nicely. When the file is saved, Unity
imports it automatically.

---

## 1. The basics

A file is a collection of **nodes**. A node is one "beat" of conversation: some text, maybe a
picture, maybe some choices. Each node starts with a `#` and a name:

```
# Meeting
[Anna]: Hello! I haven't seen you around before.
*Hi, I'm new here -> Meeting:New
*Just passing through -> Meeting:Passing
```

- `# Meeting` starts the node and names it. Names can use letters, numbers, `:`, `_` and `-`.
  The `:` has no special meaning - it's just a convention for grouping related nodes, like folders:
  `Meeting`, `Meeting:New`, `Meeting:Passing`.
- `[Anna]:` says who's talking. The name has to match a Speaker asset in the project (its display
  name or one of its aliases). Text after the speaker, and on the following lines, is what they say.
- Lines starting with `*` are **options** - choices the player can pick. The arrow `->` says which
  node each choice leads to.

Text without any speaker is allowed too - it just shows with no name attached:

```
# Intro
The rain hadn't stopped for three days.
```

A **blank line** inside a node splits the text into separate "pages": the player sees the first
part, presses continue, sees the next.

**Comments** are notes to yourself that the game never shows:

```
// this is a comment
/* this is a comment
   over several lines */
```

---

## 2. Moving between nodes

### Options

```
*Sounds good -> Path:Accept
*No, thanks  -> Path:Refuse
```

### Automatic redirects

A node that should flow straight into another one uses `=>` on its own line:

```
# Path:Accept
[Anna]: Great! Follow me.
=>Path:Walk
```

### Ending the conversation

`End` is a reserved destination: an option (or a redirect) pointing at it closes the conversation.
Any code attached to the option still runs first:

```
*(Exit) -> End

*(Exit)=>{
    AddBuff("BuffHeroism");
}->End
```

A node whose text just runs out (no options, no redirect) also ends the conversation - `-> End` is
for when an *option* should do it. Because the word is reserved, no node may be named `End`.

### Conditional redirects

A redirect can be gated by a condition. Conditions are checked top to bottom and the first one that
passes wins:

```
# Greeting
{MetAnnaBefore}=>Greeting:Again
=>Greeting:FirstTime
```

`MetAnnaBefore` here is a **variable** - see section 6.

### Random redirects

Give the redirects percentages instead of conditions and one of them is picked at random:

```
# OpenChest
{25%}=>OpenChest:Trap
{25%}=>OpenChest:Treasure
{50%}=>OpenChest:Empty
```

The numbers are relative weights - they don't have to add to 100 (`{1%}/{1%}/{2%}` behaves the
same). A weight of `{0%}` switches a branch off without deleting it.

**Important: this roll is remembered.** Once a chest has decided it's a trap, it stays a trap - the
player closing the window and clicking again gets the same result. If you *want* it re-rolled every
time (a fortune teller giving random advice, say), put `{AlwaysRoll}` on the node:

```
# FortuneTeller
{AlwaysRoll}
{50%}=>FortuneTeller:Good
{50%}=>FortuneTeller:Bad
```

### Random line instead of random node

`{Random}` on a node makes it say just *one* of its text pages, picked at random each time. Good
for repeated small talk ("barks"). This one is always re-rolled - that's the point of it.

```
# Guard:Bark
{Random}
[Guard]: Move along.

[Guard]: Nothing to see here.

[Guard]: ...
```

---

## 3. Choices with rules on them

Any option can have a **guard** written in `{ }` right before the `*`:

```
{HasRations(500)}*Give her rations (-500 rations) -> Girl:Rations
{50%}*A strange glint catches your eye -> Girl:Glint
{OneShot}*Ask about her parents -> Girl:Parents
```

- `{HasRations(500)}` - a **condition**: the option only works while this is true.
- `{50%}` - a **chance**: the option has a 50% chance of being there at all. Like the random
  redirect, the roll is remembered - the option doesn't blink in and out as the player revisits the
  node (unless the node has `{AlwaysRoll}`).
- `{OneShot}` - the option can only be picked **once**. After that it disappears. Perfect for
  question hubs where each question should only be asked once.

They can be combined, with the one-shot first, then the chance, then the condition:

```
{OneShot && 25% && HasKey}*Try the key -> Door:Key
```

### Showing options the player can't pick

Normally an option whose condition fails just isn't shown. Two node tags change that:

- `{ShowInvalid}` - options with failed conditions are shown greyed out instead of hidden. The
  player can see what the choice *would* have been. (Failed chance rolls are always hidden - a
  missed roll isn't something the player should be teased with.)
- `{ShowOneShot}` - already-used one-shot options are shown greyed out instead of vanishing.

```
# Girl:Decision
{ShowInvalid, ShowOneShot}
...
```

---

## 4. Running game code

Dialogues can change the game: add resources, set variables, spawn things. This is done with small
**code blocks**. Each statement ends with `;`. There are three places a block can go, and the place
decides *when* it runs:

### When the node is entered - `{ ... }`

Runs before the text appears. Use it when the text announces the effect, so the numbers on screen
already agree with what's being read:

```
# Crates:Loot
[Narrator]: The crates hold ammunition (+1000 ammo).
{
    AddResource("Ammo", 1000);
}
```

### When the node is left - `=>{ ... }`

Runs when the player dismisses the node, whichever option they picked. Use it for effects that
should land as the window closes:

```
# Ambush
[Narrator]: It was a trap!
=>{
    TriggerSpawner("AmbushPoint");
}
```

### When one specific option is picked - `*text =>{ ... }-> key`

The block belongs to that choice and runs only for it:

```
*Take her with us =>{
    TakeGirl = true;
    AddRecruits("RefugeeGirl", 1);
}->Girl:Taken
```

On a click the order is: the node's `=>{ }` block first, then the picked option's block, then the
jump - and then the next node's `{ }` entry block before any of its text is drawn.

### Code that must only run once - `{OneShot}{ ... }`

An entry block normally runs *every* time the node is entered. If the node hands out a reward, that
would hand it out again when the player comes back through a loop. Put `{OneShot}` right before the
block's `{` and it runs only the first time:

```
# Crates:Loot
[Narrator]: The crates hold ammunition (+1000 ammo).
{OneShot}{
    AddResource("Ammo", 1000);
}
*Look around -> Crates:Search
*Move on -> Crates:Done
```

Now the player can come back to `Crates:Loot` as often as the conversation allows - the ammo is
only given once. A node can mix one-shot and normal blocks; each `{ }` block is its own thing.

---

## 5. Node tags

Tags are written in `{ }` on their own line, right under the `#` line (several can share a line,
separated by commas). The full list:

| Tag              | Meaning |
|------------------|---------|
| `{OneShot}`      | The whole node only plays once. Trying to enter it again does nothing. |
| `{GlobalOneShot}`| Same, but "once" counts across every copy of the event (see section 7). |
| `{Random}`       | Show one random text page of this node instead of all of them in order. |
| `{ShowInvalid}`  | Options with failed conditions are greyed out instead of hidden. |
| `{ShowOneShot}`  | Spent one-shot options are greyed out instead of hidden. |
| `{AlwaysRoll}`   | This node's random rolls (weighted redirects, option chances) are re-rolled on every visit instead of being remembered. |
| `{Global}`       | Everything stateful in this node (rolls, one-shots) is shared game-wide instead of per-instance - see section 7. |
| `{Local}`        | The opposite of `{Global}` and already the default; write it when you want to be explicit. |
| `{Marker=name}`  | Names this spot so `-> Marker(name)` can jump back to it - see section 8. |

---

## 6. Variables and dynamic text

### Setting and checking variables

Code blocks can set variables, and conditions can check them. Variables don't need to be declared -
just use them:

```
# Girl:Found
{OneShot}{
    FoundGirl = true;
}

# LaterEvent
{FoundGirl}=>LaterEvent:GirlFollowup
=>LaterEvent:Normal
```

Variables can hold numbers (`Count = 3;`), true/false (`Found = true;`) and text
(`Name = "Anna";`). Conditions can compare them: `{Count >= 3}`, `{Name == "Anna"}`,
`{Found && Count > 0}`.

### Local variables

A variable like `FoundGirl` is **global** - every dialogue in the game sees it. If the same event
can exist in more than one place (see section 7), prefix the name with `local.` and each copy of
the event gets its own:

```
{OneShot}{
    local.foundGirl = true;
}
...
{local.foundGirl}*Ask about the girl -> ...
```

### Showing values in text

`${...}` anywhere in text is replaced with the value when the line is shown:

```
[Narrator]: Of the ${sentCount} soldiers who left, ${survivorCount} came back.
We lost ${sentCount - survivorCount}.
```

If the value can't be found (a typo, usually) the text shows literally as `${survivorCont}`, which
makes the mistake easy to spot in game.

### Asking about other nodes

Three built-in questions can be used in any condition:

| Function             | Answers |
|----------------------|---------|
| `Visits("Key")`      | How many times the node `Key` has been entered (a number). |
| `HasSeen("Key")`     | Whether it has been entered at all (true/false). |
| `HasRun("Key")`      | Whether its entry code has executed at least once (true/false). |

```
{Visits("Girl:Talk") >= 3}*She seems to trust you now -> Girl:Trust
{HasSeen("Ambush")}*You mention the ambush -> Camp:AmbushTalk
```

Each game also adds its own functions (things like `HasRations(500)` or `CanBuff("...")` here) -
ask your programmer what's available, or look at the context class.

---

## 7. Local vs global: what "once" means

This is the part worth reading twice.

The game can attach a separate **memory** to each thing that starts a conversation - for example,
each point of interest on the map gets its own. Everything "remembered" about a dialogue lives in
one of these memories:

- which nodes have been visited (what `{OneShot}` and `Visits()` look at),
- which way random rolls went,
- which one-shot options were used, and which one-shot code blocks ran,
- `local.` variables.

By default all of that is **local**: it belongs to the memory of the thing that started the
conversation. Two ammo-dump events on the map are two separate ammo dumps - each rolls its own
outcome, each spends its own one-shots. This is almost always what you want, so you don't have to
write anything to get it.

Sometimes you want the opposite - "this can only ever happen once in the whole game". That's what
the `Global` variants are for:

- `{GlobalOneShot}` on a node: once *any* copy has played it, no copy will play it again.
- `{GlobalOneShot}` in an option guard: the option can be picked once in the whole game.
- `{GlobalOneShot}{ ... }`: the code runs once in the whole game.
- `{Global}` on a node: shorthand - everything stateful in that node (its rolls included) is
  game-wide.

One practical example: a unique wandering merchant who appears in several random events but should
only ever join the player once:

```
{GlobalOneShot}*Invite the merchant to travel with you -> Merchant:Joins
```

(If a conversation is started without any specific memory attached, "local" quietly means the
game-wide memory - everything still works, there's just only one memory in play.)

---

## 8. Reusing dialogue from several places

### Includes

A conversation you want to use in more than one event can live in its own file. Reference it at the
top of any file that wants it:

```
include("RefugeeGirl")
```

Now every node of `RefugeeGirl.dialogue` can be jumped to as if it were in this file. The name is
the file name without the extension; a file in the same folder wins if the name exists twice.

When a jump target isn't found in the current file, the search continues in the included files (and
in whatever *they* include), and finally among the globally registered dialogues. So keep an
event's nodes in its own file, include what it borrows, and nothing needs to be registered
anywhere.

After adding or renaming files, run **Unity Common → Dialogue → Update References** from the menu.
It re-links every include and then checks the whole project: includes that don't exist, jumps to
nodes that don't exist anywhere, and node names accidentally used in two files. Run it before a
build, too - it's the thing that guarantees every include is packed into the build.

### Markers: jumping to a place only the caller knows

The included file has a problem: it can't name the node to hand control back to, because it doesn't
know who's using it. Markers solve this. The caller puts a name on one of its nodes:

```
// In AmmoDump.dialogue
# AmmoDump:GirlDone
{Marker=GirlDone}
[Narrator]: The soldiers move on, the crates behind them.
=>AmmoDump:Done
```

...and the shared file jumps to whoever declared `GirlDone`:

```
// In RefugeeGirl.dialogue
# RefugeeGirl:Leave
[Narrator]: No one looks back for very long.
*(Exit) -> Marker(GirlDone)
```

How a marker gets its meaning:

- Every `{Marker=...}` in the file the conversation **started in** - and in everything it includes -
  is registered the moment the conversation starts. So the shared file can jump "forward" to the
  caller's wrap-up node even though that node was never visited.
- The entry file's declarations win over its includes'. That means a shared file can declare a
  *fallback* node with the same marker name for when it runs on its own, and any caller overrides
  it just by declaring the marker itself.
- **Visiting** a marked node re-points the marker at it. That's what makes the classic "go back to
  where we were" work: mark the hub, wander off, `-> Marker(hub)` returns to it.

Marker names can use letters, numbers, `:`, `_` and `-`. If a shared file includes *another* shared
file, just use different marker names. Markers only live for the duration of one conversation.

### History: "go back one step"

`-> History(-1)` jumps to the previous node the player actually saw (`-2` two back, and so on;
`History(0)` restarts the current node). Going back also rewinds the trail, so going back twice
keeps walking *back* instead of bouncing between two nodes.

```
*Never mind -> History(-1)
```

A word of caution: anything further back than `-1` depends on the path the *player* took, which you
can't predict from the file - in a hub where they can wander, `History(-2)` lands in different
places for different players. For "return to a known place", markers are the reliable tool; history
is for "undo one step".

---

## 9. Attributes: extra info for the game

Lines starting with `@` attach named values to a node's text. The dialogue system itself ignores
them - each game decides what they mean (image to show, title, screen shake...):

```
# AmmoDump
@title=Ammo Drop
@image=evt_ammo_crates
@tooltip=Some military-looking crates
[Narrator]: The soldiers approach the crates cautiously.
```

A bare `@name` with no value means "yes" - handy for on/off things like `@skippable`.

---

## 10. Recipes

### A question hub where each question can be asked once

```
# Girl:Talk
{ShowOneShot}
[Narrator]: She answers in short, hesitant fragments.
{OneShot}*Where are your parents? -> Girl:Parents
{OneShot}*What happened to you? -> Girl:Attack
{OneShot}*Have you seen demons nearby? -> Girl:Patrols
*That's enough for now -> Girl:Decision

# Girl:Parents
[RefugeeGirl]: We were going to Beacon. Me, Mum, Dad...
=>Girl:Talk

# Girl:Attack
[RefugeeGirl]: Imps came out of the rocks...
=>Girl:Talk

# Girl:Patrols
[RefugeeGirl]: Sometimes. Just imps, two or three at a time.
=>Girl:Talk
```

Each answer flows back to the hub; asked questions grey out one by one.

### An event that hands out a reward exactly once, even with loops

```
# Crates
[Narrator]: The crates hold ammunition (+1000 ammo).
{OneShot}{
    AddResource("Ammo", 1000);
}
*Search the rest -> Crates:Search
*Leave -> Crates:Done

# Crates:Search
[Narrator]: Nothing else of value.
=>Crates            // safe: the +1000 won't repeat
```

### A random outcome that commits

```
# Dig
{30%}=>Dig:Treasure
{70%}=>Dig:Nothing
```

Nothing else needed - once rolled, this spot is what it rolled. Add `{AlwaysRoll}` only if you
*want* it different every time.

### A conversation node that changes on repeat visits

```
# Guard
{HasSeen("Guard:Intro") == false}=>Guard:Intro
{Visits("Guard") > 4}=>Guard:Annoyed
=>Guard:Normal
```

### One choice, three timings

```
# Farewell
[Anna]: Take this. And... be careful out there.
{                       // on entering: before the text shows
    MetAnna = true;
}
=>{                     // on leaving: whatever gets picked
    AddResource("Gold", 10);
}
*Thank you =>{          // only when THIS option is picked
    AnnaFriendship = 1;
}->Farewell:End
*Take it silently -> Farewell:End
```

---

## 11. When something doesn't work

- **The console is your friend.** Bad lines don't crash anything - they print a warning with a
  clickable link to the exact file and line.
- **A jump goes nowhere / "dialogue not found":** run *Unity Common → Dialogue → Update
  References*. It lists every broken jump and unresolved include in the project.
- **Text shows `${something}` literally:** the variable name is misspelled, or it was never set.
- **An option never appears:** check its condition, and remember chance rolls are remembered - a
  `{50%}` option that rolled "no" stays gone for that instance unless the node has `{AlwaysRoll}`.
- **Code runs twice / doesn't run:** check whether the block is entry (`{`), exit (`=>{`) or
  option code, and whether it should be `{OneShot}{`.
