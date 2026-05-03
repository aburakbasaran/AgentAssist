# ADR-0006: AwesomeAssertions Over FluentAssertions

Phase A uses AwesomeAssertions instead of FluentAssertions so tests keep the familiar fluent assertion style while aligning with the requested MIT-licensed FluentAssertions community fork decision; all test projects reference `AwesomeAssertions` through Central Package Management and use the `AwesomeAssertions` namespace.
