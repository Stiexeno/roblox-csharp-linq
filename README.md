# roblox-csharp-linq

LINQ-to-Objects for [roblox-csharp](https://github.com/Stiexeno/roblox-csharp). Write `nums.Where(n => n > 0).Select(n => n * 2).ToList()` and have it work, exactly like in `dotnet`.

## Install

From your roblox-csharp project root (requires roblox-csharp `0.1.0-alpha.25` or newer):

```sh
roblox-csharp plugin add Stiexeno/roblox-csharp-linq
```

That drops the plugin into `plugins/Linq/`. The runtime mounts at `ReplicatedStorage.Plugins.Linq.Enumerable`. No `using` change needed — `System.Linq.Enumerable` is in the BCL, and the transpiler routes its calls into this plugin automatically.

## Quick start

```csharp
using System.Collections.Generic;
using System.Linq;

public class Scoreboard
{
    public List<Player> TopThree(List<Player> players)
    {
        return players
            .Where(p => p.IsAlive)
            .OrderByDescending(p => p.Score)
            .Take(3)
            .ToList();
    }
}
```

## Method coverage (v0.1.0)

| Category | Methods |
|---|---|
| Projection | `Select`, `SelectMany` |
| Filtering | `Where`, `Distinct` |
| Materializing | `ToList`, `ToArray` |
| Quantifiers | `Any`, `All`, `Contains` |
| Element | `First`, `FirstOrDefault`, `Last`, `LastOrDefault` |
| Counting | `Count` (with and without predicate) |
| Aggregation | `Sum`, `Min`, `Max`, `Aggregate` |
| Ordering | `OrderBy`, `OrderByDescending`, `Reverse` |
| Slicing | `Take`, `Skip` |
| Concatenation | `Concat` |

## What's not in v1

- **Lazy evaluation.** Each chained method materializes a new table. Chains like `.Where(...).Select(...).First()` allocate intermediates instead of fusing into one walk. For typical game-code list sizes this is unnoticeable; if you have a multi-million-element source you should re-walk by hand.
- **`GroupBy`, `Join`, `ToDictionary`, `ToLookup`.** Need grouping primitives (`IGrouping`, `ILookup`) that aren't in v1.
- **`Zip`, `ElementAt`, `Range`, `Repeat`.** Less common; add when you hit one.
- **Custom comparers / equality.** `Distinct` and ordering use Luau's built-in `==` / `<` only.
- **`ThenBy`.** Chained ordering after `OrderBy`; not in v1.
- **Async LINQ** (`IAsyncEnumerable`). Roblox doesn't have a native async-enumerable concept.

## License

[MIT](LICENSE).
