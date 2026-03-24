namespace Intentify.Modules.Engage.Application;

internal static class EngageInputExtractionSignalBank
{
    internal static readonly string[] ExplicitNamePrefixes =
    [
        "my name is",
        "i am",
        "i'm",
        "im"
    ];

    internal static readonly string[] LocationMarkers =
    [
        " in ",
        " at ",
        " from ",
        " near ",
        " around "
    ];

    internal static readonly string[] NonNameContextTerms =
    [
        "office",
        "store",
        "business",
        "location",
        "address",
        "city",
        "country",
        "state",
        "zip",
        "postal",
        "website",
        "service",
        "project",
        "timeline",
        "budget"
    ];
}
