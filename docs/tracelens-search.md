# TraceLens Search Filters

The `SearchTraces` gRPC/MCP command accepts a composable filter expression so callers can
combine service, span name, and attribute predicates without chaining multiple requests.

## Request shape

```proto
message SearchTracesRequest {
  TraceFilterExpression filter = 9;
  uint64 start_time = 5;
  uint64 end_time = 6;
  int32 limit = 7;
  LogFilter log_filter = 10;
}
```

- `filter` is a tree of `TraceFilterExpression` nodes. Each node is either:
  - a composite (`TraceFilterComposite`) combining child expressions with `OPERATOR_AND` or `OPERATOR_OR`;
  - a leaf predicate (`ServiceFilter`, `SpanNameFilter`, or `AttributeFilter`).
- `LogFilter` retains the previous substring search for log bodies associated with the returned traces.

Attribute predicates default to equality when a value is supplied and fall back to an
"exists" check when only a key is provided. Set `target` to `ATTRIBUTE_FILTER_TARGET_SPAN`
(the default) to search span attributes; additional targets can be introduced without
changing the contract.

## Examples

Search for traces from `checkout-service` where any span has either
`http.method=GET` *or* `http.method=POST`:

```json
{
  "limit": 20,
  "filter": {
    "composite": {
      "operator": "OPERATOR_AND",
      "expressions": [
        { "service": { "name": "checkout-service" } },
        {
          "composite": {
            "operator": "OPERATOR_OR",
            "expressions": [
              { "attribute": { "key": "http.method", "value": "GET" } },
              { "attribute": { "key": "http.method", "value": "POST" } }
            ]
          }
        }
      ]
    }
  }
}
```

Add an additional `attribute` child under an `OPERATOR_AND` node to require multiple
key/value pairs on the same span. Combine composites for richer boolean logic without
introducing custom SQL per client.
