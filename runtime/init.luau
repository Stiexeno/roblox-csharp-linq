-- Entry point. Only one module to expose — the rest of LINQ would
-- live alongside Enumerable here if we add IGrouping, ILookup, etc.

return {
	Enumerable = require(script.Enumerable),
}
