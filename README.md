# Automapper OData $expand example
A short project demonstrating how one could use Automapper with $expand and have the result collection include $inlinecount and nextpagequery links.

# Idea
Automapper includes great support for OData in that you can just do an [EnableQuery] and then ProjectTo<> inside your controller code, and $order, $top, $skip, $select and $filter work out of the box.

What was missing is the next page link (think HATEOAS), total count and the ability to skip expanding the navigational properties.
By default, Automapper supports .ExplicitExpansion() in the Map, so all we needed to do was add translation from OData $expand to the relevant call of ProjectTo<>.
