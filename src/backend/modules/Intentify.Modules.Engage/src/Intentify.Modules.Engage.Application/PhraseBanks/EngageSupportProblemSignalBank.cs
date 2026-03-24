namespace Intentify.Modules.Engage.Application;

internal static class EngageSupportProblemSignalBank
{
    internal static readonly string[] ProblemPhrases =
    [
        // General failures
        "not working",
        "isn't working",
        "is not working",
        "doesn't work",
        "doesnt work",
        "stopped working",
        "no longer working",
        "broken",
        "failed",
        "keeps failing",
        "not responding",
        "unresponsive",
        "crashed",
        "freezing",
        "stuck",
        "glitch",
        "bug",

        // UI / page issues
        "page not loading",
        "page won't load",
        "page is blank",
        "blank page",
        "white screen",
        "black screen",
        "nothing shows",
        "nothing loading",
        "screen is empty",
        "content not loading",
        "page keeps refreshing",
        "page stuck loading",

        // Element issues
        "button not working",
        "link broken",
        "link not working",
        "click not working",
        "dropdown not working",
        "menu not working",
        "form not working",
        "form not submitting",
        "form won't submit",
        "submit button not working",

        // Contact / form issues
        "contact page isn't working",
        "contact page is not working",
        "contact form not working",
        "cannot submit",
        "can't submit",
        "cant submit",
        "submission failed",

        // Media / assets
        "image not showing",
        "images not loading",
        "video not playing",
        "media not loading",
        "map not showing",
        "map not loading",

        // Clarity / UX problems
        "directions not clear",
        "information not clear",
        "prices not clear",
        "pricing not clear",
        "confusing",
        "very confusing",
        "unclear",
        "not clear",
        "hard to understand",
        "difficult to understand",
        "doesn't make sense",
        "makes no sense",

        // Checkout / payment issues
        "checkout not working",
        "payment failed",
        "payment not going through",
        "payment declined",
        "transaction failed",
        "unable to pay",
        "card not working",
        "checkout error",
        "order failed",
        "order not processed",

        // Refund / billing issues
        "refund issue",
        "refund not received",
        "no refund",
        "charged twice",
        "double charged",
        "incorrect charge",
        "billing issue",
        "invoice issue",

        // Auth / account issues
        "cannot log in",
        "can't log in",
        "cant log in",
        "login not working",
        "sign in not working",
        "password not working",
        "reset password not working",
        "verification failed",
        "code not received",
        "otp not received",
        "account locked",
        "access denied",
        "cannot access account",

        // Upload / file issues
        "can't upload",
        "cant upload",
        "upload failed",
        "file upload not working",
        "cannot upload file",

        // Performance issues
        "slow",
        "very slow",
        "too slow",
        "loading slowly",
        "takes too long",
        "lagging",
        "delay",
        "timeout",
        "timed out",

        // API / system errors
        "server error",
        "internal error",
        "500 error",
        "404 error",
        "403 error",
        "bad request",
        "service unavailable",
        "gateway error",

        // Mobile / device issues
        "not working on mobile",
        "not working on phone",
        "not working on tablet",
        "mobile view broken",
        "layout broken on mobile",
        "screen not responsive",

        // General dissatisfaction signals
        "this is not working properly",
        "this doesn't work",
        "this is broken",
        "this is frustrating",
        "this is annoying",
        "this is useless",
        "not helpful",
        "waste of time"
    ];

    internal static readonly string[] ProblemTerms =
    [
        "error",
        "issue",
        "problem",
        "failure",
        "failed",
        "broken",
        "bug",
        "glitch",
        "confusing",
        "unclear",
        "blank",
        "slow",
        "lag",
        "delay",
        "timeout",
        "crash",
        "freeze",
        "unresponsive",
        "denied",
        "invalid"
    ];

    internal static readonly string[] SurfaceTerms =
    [
        // Core surfaces
        "site",
        "website",
        "app",
        "application",
        "platform",
        "system",

        // Pages / navigation
        "page",
        "screen",
        "dashboard",
        "home page",
        "landing page",

        // UI elements
        "link",
        "button",
        "menu",
        "dropdown",
        "form",
        "field",
        "input",
        "submit",
        "search",

        // Contact / forms
        "contact",
        "contact page",
        "contact form",

        // Commerce surfaces
        "checkout",
        "cart",
        "payment",
        "billing",
        "invoice",
        "refund",
        "order",
        "subscription",

        // Media / maps
        "image",
        "images",
        "video",
        "map",
        "directions",

        // Info surfaces
        "information",
        "details",
        "prices",
        "pricing",
        "content",

        // Auth / user
        "login",
        "log in",
        "sign in",
        "account",
        "profile",
        "password",
        "verification",
        "code",

        // File handling
        "upload",
        "file",
        "attachment",

        // Technical surfaces
        "api",
        "server",
        "endpoint",
        "database",

        // Device context
        "mobile",
        "phone",
        "tablet",
        "browser"
    ];
}