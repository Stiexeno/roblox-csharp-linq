-- C# System.Linq.Enumerable, the Luau translation. Every method takes
-- the source sequence as its first positional argument (the same shape
-- the transpiler emits after reducing the `seq.Method(...)` extension
-- form). Results are eager — each method materializes a new table
-- rather than yielding a lazy iterator, which trades C#'s composed-
-- enumerable laziness for a simpler model that doesn't depend on
-- coroutine plumbing for chained calls.
--
-- Predicate / selector functions receive the value only (not the
-- index) to match C#'s Func<T, R> signature.

local Enumerable = {}

-- Walks `source` in array-iteration order. The default lowering for
-- C# collections produces array-indexed Luau tables, so ipairs is
-- correct; List<T> / array / IEnumerable<T> from C# user code all
-- look the same downstream.

function Enumerable.Select(source, selector)
	local result = {}
	for i, value in ipairs(source) do
		result[i] = selector(value)
	end
	return result
end

function Enumerable.Where(source, predicate)
	local result = {}
	for _, value in ipairs(source) do
		if predicate(value) then
			table.insert(result, value)
		end
	end
	return result
end

-- Both Count overloads collapse into one: with a predicate, count
-- matches; without, count all. Distinguish by checking the arity.

function Enumerable.Count(source, predicate)
	if predicate == nil then
		return #source
	end
	local n = 0
	for _, value in ipairs(source) do
		if predicate(value) then n += 1 end
	end
	return n
end

function Enumerable.Any(source, predicate)
	if predicate == nil then
		return #source > 0
	end
	for _, value in ipairs(source) do
		if predicate(value) then return true end
	end
	return false
end

function Enumerable.All(source, predicate)
	for _, value in ipairs(source) do
		if not predicate(value) then return false end
	end
	return true
end

function Enumerable.First(source, predicate)
	if predicate == nil then
		if #source == 0 then
			error("[Linq] First: sequence is empty", 2)
		end
		return source[1]
	end
	for _, value in ipairs(source) do
		if predicate(value) then return value end
	end
	error("[Linq] First: no element matches the predicate", 2)
end

-- C# returns default(T) — which for reference types is null, for
-- value types is the type's zero. In Luau we return nil for any
-- "no element" case; the caller is responsible for handling nil.

function Enumerable.FirstOrDefault(source, predicate)
	if predicate == nil then
		return source[1]
	end
	for _, value in ipairs(source) do
		if predicate(value) then return value end
	end
	return nil
end

function Enumerable.Last(source, predicate)
	if predicate == nil then
		if #source == 0 then
			error("[Linq] Last: sequence is empty", 2)
		end
		return source[#source]
	end
	for i = #source, 1, -1 do
		if predicate(source[i]) then return source[i] end
	end
	error("[Linq] Last: no element matches the predicate", 2)
end

function Enumerable.LastOrDefault(source, predicate)
	if predicate == nil then
		return source[#source]
	end
	for i = #source, 1, -1 do
		if predicate(source[i]) then return source[i] end
	end
	return nil
end

function Enumerable.Sum(source, selector)
	local sum = 0
	if selector == nil then
		for _, value in ipairs(source) do sum += value end
	else
		for _, value in ipairs(source) do sum += selector(value) end
	end
	return sum
end

-- Min / Max iterate once and assume the values are comparable with
-- Luau's `<` / `>`. With a selector, the selector's result is what's
-- compared; the original element is returned to match C#'s behavior
-- (Min(selector) actually returns the SELECTED value in BCL, even
-- though that's surprising — we follow that convention for parity).

function Enumerable.Min(source, selector)
	if #source == 0 then error("[Linq] Min: sequence is empty", 2) end
	local best = selector and selector(source[1]) or source[1]
	for i = 2, #source do
		local value = selector and selector(source[i]) or source[i]
		if value < best then best = value end
	end
	return best
end

function Enumerable.Max(source, selector)
	if #source == 0 then error("[Linq] Max: sequence is empty", 2) end
	local best = selector and selector(source[1]) or source[1]
	for i = 2, #source do
		local value = selector and selector(source[i]) or source[i]
		if value > best then best = value end
	end
	return best
end

function Enumerable.Contains(source, item)
	for _, value in ipairs(source) do
		if value == item then return true end
	end
	return false
end

-- ToList / ToArray return a shallow copy. C# materializes any
-- IEnumerable<T> here; in Luau our intermediate results are already
-- arrays so this is just a defensive copy that lets the caller
-- mutate without affecting the source.

function Enumerable.ToList(source)
	local result = {}
	for i, value in ipairs(source) do result[i] = value end
	return result
end

function Enumerable.ToArray(source)
	return Enumerable.ToList(source)
end

function Enumerable.Take(source, count)
	local result = {}
	local n = math.min(count, #source)
	for i = 1, n do result[i] = source[i] end
	return result
end

function Enumerable.Skip(source, count)
	local result = {}
	for i = count + 1, #source do
		table.insert(result, source[i])
	end
	return result
end

function Enumerable.Reverse(source)
	local result = {}
	local n = #source
	for i = 1, n do result[i] = source[n - i + 1] end
	return result
end

-- Distinct keeps the first occurrence of each value. Uses Luau's
-- table-as-set; works for primitives (numbers, strings, booleans)
-- and reference-equal tables / instances. User-defined "equals"
-- with a custom comparer isn't supported in v1.

function Enumerable.Distinct(source)
	local seen = {}
	local result = {}
	for _, value in ipairs(source) do
		if not seen[value] then
			seen[value] = true
			table.insert(result, value)
		end
	end
	return result
end

function Enumerable.Concat(first, second)
	local result = {}
	for i, value in ipairs(first) do result[i] = value end
	local offset = #first
	for i, value in ipairs(second) do result[offset + i] = value end
	return result
end

-- OrderBy / OrderByDescending sort by the result of the selector.
-- Sorts a copy to avoid mutating the caller's source — matches C#'s
-- contract that LINQ never mutates its input.

function Enumerable.OrderBy(source, selector)
	local result = Enumerable.ToList(source)
	table.sort(result, function(a, b) return selector(a) < selector(b) end)
	return result
end

function Enumerable.OrderByDescending(source, selector)
	local result = Enumerable.ToList(source)
	table.sort(result, function(a, b) return selector(a) > selector(b) end)
	return result
end

function Enumerable.Aggregate(source, seedOrFunc, func)
	-- C# has two overloads:
	--   Aggregate(source, accumulator)            -- seed = first element
	--   Aggregate(source, seed, accumulator)      -- explicit seed
	-- Disambiguate by argument count: if func is nil, seedOrFunc is
	-- the accumulator and we seed from source[1].
	if func == nil then
		local accumulator = seedOrFunc
		if #source == 0 then
			error("[Linq] Aggregate: sequence is empty (need a seed for empty input)", 2)
		end
		local result = source[1]
		for i = 2, #source do
			result = accumulator(result, source[i])
		end
		return result
	end
	local result = seedOrFunc
	for _, value in ipairs(source) do
		result = func(result, value)
	end
	return result
end

-- SelectMany flattens one level — selector returns a sequence per
-- element, and the result concatenates them.

function Enumerable.SelectMany(source, selector)
	local result = {}
	for _, value in ipairs(source) do
		local inner = selector(value)
		for _, innerValue in ipairs(inner) do
			table.insert(result, innerValue)
		end
	end
	return result
end

return Enumerable
