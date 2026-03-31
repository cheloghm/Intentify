using Intentify.Modules.Engage.Domain;
using System.Text.RegularExpressions;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageConversationPolicy
{
    private static readonly EngageInputInterpreter Interpreter = new();
    private static readonly EngageSmalltalkSignalMatcher Smalltalk = new(Interpreter);
    private static readonly EngageSupportSignalMatcher Support = new(Interpreter);
    private static readonly EngageCommercialSignalMatcher Commercial = new();

    public bool TryBuildSmalltalkResponse(string message, bool priorAssistantAskedQuestion, string greetingResponse, string ackResponse, out string response)
        => Smalltalk.TryBuildSmalltalkResponse(message, priorAssistantAskedQuestion, greetingResponse, ackResponse, out response);

    public bool IsContinuationReply(string message) => Smalltalk.IsContinuationReply(message);
    public bool IsStrongCommercialIntent(string message) => Commercial.IsStrongCommercialIntent(message);
    public bool IsExplicitCommercialContactRequest(string message) => Commercial.IsExplicitCommercialContactRequest(message);
    public bool IsRecommendationIntent(string normalizedMessage) => Commercial.IsRecommendationIntent(normalizedMessage);
    public bool TryBuildCommercialIntentContactPrompt(string message, string prefix, out string prompt)
        => Commercial.TryBuildCommercialIntentContactPrompt(message, prefix, out prompt);

    public int ComputeCommercialIntentScore(EngageChatSession session)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(session.CaptureGoal)) score += 20;
        if (!string.IsNullOrWhiteSpace(session.CaptureType)) score += 15;
        if (!string.IsNullOrWhiteSpace(session.CaptureLocation)) score += 10;
        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints)) score += 10;
        if (!string.IsNullOrWhiteSpace(session.CapturedName)) score += 10;
        if (!string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod)) score += 10;
        if (!string.IsNullOrWhiteSpace(session.CapturedEmail) || !string.IsNullOrWhiteSpace(session.CapturedPhone)) score += 25;
        return Math.Clamp(score, 0, 100);
    }

    public bool HasSufficientDiscoveryContext(EngageChatSession session)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(session.CaptureGoal)) count++;
        if (!string.IsNullOrWhiteSpace(session.CaptureType)) count++;
        if (!string.IsNullOrWhiteSpace(session.CaptureLocation)) count++;
        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints)) count++;
        return count >= 2;
    }

    public string BuildNextDiscoveryQuestion(EngageChatSession session)
    {
        var missing = DeterminePrimaryMissingField(session);
        return missing switch
        {
            "goal" => "What are you trying to achieve first?",
            "type" => "What kind of business is this for?",
            "location" => "What location should we plan for?",
            "constraints" => "Any key constraints like budget or timeline?",
            "name" => "Could I get your first name?",
            "method" => "What’s the best contact method for follow-up — email or phone?",
            "email" => "Great — what’s the best email address to reach you?",
            "phone" => "Great — what’s the best phone number to reach you?",
            "contact" => "Could you share your best email or phone number for follow-up?",
            _ => "Anything else you’d like help with?"
        };
    }

    public bool IsCommercialCaptureReady(EngageChatSession session, bool explicitContactRequest)
    {
        var fields = 0;
        if (!string.IsNullOrWhiteSpace(session.CaptureGoal)) fields++;
        if (!string.IsNullOrWhiteSpace(session.CaptureType)) fields++;
        if (!string.IsNullOrWhiteSpace(session.CaptureLocation)) fields++;
        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints)) fields++;
        return explicitContactRequest || fields >= 2;
    }

    public bool NeedsHumanHelp(string message) => Support.NeedsHumanHelp(message);
    public bool IsExplicitEscalationRequest(string message) => Support.IsExplicitEscalationRequest(message);

    public bool IsConversationCloseSignal(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var normalized = Interpreter.NormalizeUserMessage(message);
        if (EngageConversationClosePhraseBank.ClosePhrases.Any(item =>
                string.Equals(Interpreter.NormalizeUserMessage(item), normalized, StringComparison.Ordinal)))
        {
            return true;
        }

        var compact = normalized.Replace(" ", string.Empty, StringComparison.Ordinal);
        return EngageConversationClosePhraseBank.ClosePhrases.Any(item =>
        {
            var phraseNormalized = Interpreter.NormalizeUserMessage(item);
            return !string.IsNullOrWhiteSpace(phraseNormalized)
                   && string.Equals(phraseNormalized.Replace(" ", string.Empty, StringComparison.Ordinal), compact, StringComparison.Ordinal);
        });
    }

    public bool IsSupportCaptureComplete(EngageChatSession session)
    {
        var hasIssue = !string.IsNullOrWhiteSpace(session.CaptureContext);
        var hasMethod = !string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod);
        var hasContactDetail = !string.IsNullOrWhiteSpace(session.CapturedEmail) || !string.IsNullOrWhiteSpace(session.CapturedPhone);
        return hasIssue && hasMethod && hasContactDetail;
    }

    public string BuildSupportCapturePrompt(EngageChatSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CaptureContext))
        {
            session.LastAssistantAskType = "support_issue";
            return "Got it — what issue are you running into?";
        }

        if (string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod))
        {
            session.LastAssistantAskType = "contact_method";
            return "Thanks — what’s the best way for our team to contact you (email or phone)?";
        }

        if (string.Equals(session.CapturedPreferredContactMethod, "Email", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(session.CapturedEmail))
        {
            session.LastAssistantAskType = "email";
            return "Thanks — what’s the best email address to reach you?";
        }

        if (string.Equals(session.CapturedPreferredContactMethod, "Phone", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(session.CapturedPhone))
        {
            session.LastAssistantAskType = "phone";
            return "Thanks — what’s the best phone number to reach you?";
        }

        session.LastAssistantAskType = "none";
        return "Thanks — I’ve captured that. A teammate will follow up shortly. Anything else I can help with?";
    }

    public bool TryMergeShortReplySlots(EngageChatSession session, string message, string? lastAssistantQuestion)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var updated = false;
        var normalized = Interpreter.NormalizeUserMessage(message);
        var email = Interpreter.TryExtractEmail(message);
        var phone = Interpreter.TryExtractPhone(message);

        if (!string.IsNullOrWhiteSpace(email) && !string.Equals(session.CapturedEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            session.CapturedEmail = email;
            updated = true;
        }

        if (!string.IsNullOrWhiteSpace(phone) && !string.Equals(session.CapturedPhone, phone, StringComparison.OrdinalIgnoreCase))
        {
            session.CapturedPhone = phone;
            updated = true;
        }

        var contactMethod = Interpreter.TryExtractPreferredContactMethod(message, email, phone);
        if (!string.IsNullOrWhiteSpace(contactMethod)
            && !string.Equals(session.CapturedPreferredContactMethod, contactMethod, StringComparison.OrdinalIgnoreCase))
        {
            session.CapturedPreferredContactMethod = contactMethod;
            updated = true;
        }

        var askType = session.LastAssistantAskType ?? string.Empty;
        var lastQuestion = lastAssistantQuestion ?? string.Empty;

        if (askType.Contains("name", StringComparison.OrdinalIgnoreCase)
            || lastQuestion.Contains("name", StringComparison.OrdinalIgnoreCase))
        {
            var name = Interpreter.TryExtractName(message, email, phone);
            if (!string.IsNullOrWhiteSpace(name) && name.Length <= 40)
            {
                session.CapturedName = name;
                updated = true;
            }
        }

        if ((askType.Contains("goal", StringComparison.OrdinalIgnoreCase)
             || lastQuestion.Contains("trying to achieve", StringComparison.OrdinalIgnoreCase))
            && string.IsNullOrWhiteSpace(session.CaptureGoal)
            && normalized.Length >= 2)
        {
            session.CaptureGoal = message.Trim();
            updated = true;
        }

        if ((askType.Contains("type", StringComparison.OrdinalIgnoreCase)
             || lastQuestion.Contains("kind of business", StringComparison.OrdinalIgnoreCase))
            && string.IsNullOrWhiteSpace(session.CaptureType)
            && normalized.Length >= 2)
        {
            session.CaptureType = message.Trim();
            updated = true;
        }

        if ((askType.Contains("location", StringComparison.OrdinalIgnoreCase)
             || lastQuestion.Contains("location", StringComparison.OrdinalIgnoreCase))
            && string.IsNullOrWhiteSpace(session.CaptureLocation)
            && normalized.Length >= 2)
        {
            session.CaptureLocation = message.Trim();
            updated = true;
        }

        if ((askType.Contains("constraints", StringComparison.OrdinalIgnoreCase)
             || lastQuestion.Contains("budget", StringComparison.OrdinalIgnoreCase)
             || lastQuestion.Contains("timeline", StringComparison.OrdinalIgnoreCase))
            && string.IsNullOrWhiteSpace(session.CaptureConstraints)
            && normalized.Length >= 2)
        {
            session.CaptureConstraints = message.Trim();
            updated = true;
        }

        updated |= TryInferBusinessSlots(session, normalized, message);

        return updated;
    }

    public bool TryApplyStageContinuation(EngageChatSession session, string message, string? lastAssistantQuestion)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var updated = TryMergeShortReplySlots(session, message, lastAssistantQuestion);

        if (IsContextRecoverySignal(message))
        {
            return true;
        }

        var normalized = Interpreter.NormalizeUserMessage(message);
        if (normalized.Contains("just browsing", StringComparison.Ordinal)
            || normalized.Contains("maybe later", StringComparison.Ordinal)
            || normalized.Contains("not ready", StringComparison.Ordinal))
        {
            session.CaptureContext = string.IsNullOrWhiteSpace(session.CaptureContext)
                ? $"hesitation: {message.Trim()}"
                : $"{session.CaptureContext}; hesitation: {message.Trim()}";
            updated = true;
        }

        return updated;
    }

    public bool IsContextRecoverySignal(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var normalized = Interpreter.NormalizeUserMessage(message);
        return EngageContextRecoveryPhraseBank.AlreadyToldYouPhrases.Any(item => normalized.Contains(item, StringComparison.Ordinal));
    }

    public bool IsNarrowObjectionSignal(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var normalized = Interpreter.NormalizeUserMessage(message);
        return normalized.Contains("too expensive", StringComparison.Ordinal)
            || normalized.Contains("not interested", StringComparison.Ordinal)
            || normalized.Contains("not now", StringComparison.Ordinal)
            || normalized.Contains("maybe later", StringComparison.Ordinal)
            || normalized.Contains("just browsing", StringComparison.Ordinal);
    }

    public string BuildNarrowObjectionFollowUp(EngageChatSession session)
        => string.IsNullOrWhiteSpace(session.CaptureGoal)
            ? "No pressure at all — what outcome would make this worth revisiting for you?"
            : "Totally fair. What would need to be true for this to feel worth doing now?";

    public string BuildContextRecoveryPrompt(EngageChatSession session)
    {
        var missing = DeterminePrimaryMissingField(session);
        return missing switch
        {
            "goal" => "Got it — I have your context so far. What are you trying to achieve first?",
            "type" => $"You already shared your goal ({session.CaptureGoal}). What kind of business is this for?",
            "location" => "I have your goal and use case noted. What location should we plan for?",
            "constraints" => "Thanks — I have the basics. Any key constraints like budget or timeline?",
            "name" => "Thanks — could I get your first name?",
            "method" => "Thanks — what’s the best contact method for follow-up?",
            "email" => "Perfect — what’s the best email address to reach you?",
            "phone" => "Perfect — what’s the best phone number to reach you?",
            _ => "I’ve got the main details. Anything else you want to explore?"
        };
    }

    public string BuildNaturalNextQuestion(EngageChatSession session, EngageConversationContext ctx)
    {
        var missing = DeterminePrimaryMissingField(session);
        session.LastAssistantAskType = missing ?? "none";
        return BuildNextDiscoveryQuestion(session);
    }

    public bool ShouldReopenCompletedConversation(EngageChatSession session, string userMessage)
    {
        if (!session.IsConversationComplete)
        {
            return false;
        }

        var normalized = Interpreter.NormalizeUserMessage(userMessage);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (IsConversationCloseSignal(userMessage) || IsContinuationReply(normalized))
        {
            return false;
        }

        return normalized is "hi" or "hello" or "hey"
               || normalized.Contains("service", StringComparison.Ordinal)
               || normalized.Contains("offer", StringComparison.Ordinal)
               || normalized.Contains("pricing", StringComparison.Ordinal)
               || normalized.Contains("help", StringComparison.Ordinal)
               || normalized.Contains("need", StringComparison.Ordinal)
               || normalized.Contains("question", StringComparison.Ordinal)
               || normalized.Contains("website", StringComparison.Ordinal);
    }

    public string DeterminePrimaryMissingField(EngageChatSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CaptureGoal)) return "goal";
        if (string.IsNullOrWhiteSpace(session.CaptureType)) return "type";
        if (string.IsNullOrWhiteSpace(session.CaptureLocation)) return "location";
        if (string.IsNullOrWhiteSpace(session.CaptureConstraints)) return "constraints";
        if (string.IsNullOrWhiteSpace(session.CapturedName)) return "name";
        if (string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod)) return "method";

        if (string.Equals(session.CapturedPreferredContactMethod, "Email", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(session.CapturedEmail)) return "email";

        if (string.Equals(session.CapturedPreferredContactMethod, "Phone", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(session.CapturedPhone)) return "phone";

        if (string.IsNullOrWhiteSpace(session.CapturedEmail) && string.IsNullOrWhiteSpace(session.CapturedPhone)) return "contact";

        return "none";
    }

    public bool IsServiceQuestion(string userMessage)
    {
        var normalized = Interpreter.NormalizeUserMessage(userMessage);
        return normalized.Contains("service", StringComparison.Ordinal)
               || normalized.Contains("offer", StringComparison.Ordinal)
               || normalized.Contains("pricing", StringComparison.Ordinal)
               || normalized.Contains("cost", StringComparison.Ordinal)
               || normalized.Contains("what do you", StringComparison.Ordinal);
    }

    public string BuildGroundedKnowledgeAnswer(string knowledgeSummary, string userMessage)
    {
        var lines = knowledgeSummary
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        if (lines.Length == 0)
        {
            return "Happy to help — could you share what you’re looking for so I can point you to the right service?";
        }

        var opening = IsServiceQuestion(userMessage)
            ? "Here’s what we can help with:"
            : "Here’s what I found:";

        if (lines.Length == 1)
        {
            return $"{opening} {lines[0]}";
        }

        return $"{opening} {string.Join(" ", lines)}";
    }


    public bool IsAcknowledgementTurn(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var normalized = Interpreter.NormalizeUserMessage(message);
        return normalized is "ok" or "okay" or "okay good" or "yes" or "yes please" or "please do" or "go ahead" or "sounds good" or "that works"
               || normalized.StartsWith("yes ", StringComparison.Ordinal)
               || normalized.StartsWith("ok ", StringComparison.Ordinal)
               || normalized.StartsWith("okay ", StringComparison.Ordinal)
               || normalized.Contains("go ahead", StringComparison.Ordinal)
               || normalized.Contains("please do", StringComparison.Ordinal);
    }

    public bool IsPricingIntent(string message)
    {
        var normalized = Interpreter.NormalizeUserMessage(message);
        return normalized.Contains("how much", StringComparison.Ordinal)
               || normalized.Contains("cost", StringComparison.Ordinal)
               || normalized.Contains("pricing", StringComparison.Ordinal)
               || normalized.Contains("estimate", StringComparison.Ordinal)
               || normalized.Contains("quote", StringComparison.Ordinal);
    }

    public bool HasSufficientPricingContext(EngageChatSession session)
    {
        return !string.IsNullOrWhiteSpace(session.CaptureType)
               && (!string.IsNullOrWhiteSpace(session.CaptureGoal) || !string.IsNullOrWhiteSpace(session.CaptureContext));
    }

    public string BuildScopedEstimate(EngageChatSession session, IReadOnlyCollection<EngageChatMessage> recentMessages)
    {
        var features = ExtractFeatureScope(session, recentMessages);
        var hasDelivery = features.Contains("delivery", StringComparison.OrdinalIgnoreCase) || features.Contains("ordering", StringComparison.OrdinalIgnoreCase);
        var hasCatering = features.Contains("catering", StringComparison.OrdinalIgnoreCase);

        var low = 1800;
        var high = 3500;

        if (hasDelivery)
        {
            low += 700;
            high += 1800;
        }

        if (hasCatering)
        {
            low += 400;
            high += 900;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints))
        {
            var normalizedConstraints = Interpreter.NormalizeUserMessage(session.CaptureConstraints);
            if (normalizedConstraints.Contains("week", StringComparison.Ordinal) || normalizedConstraints.Contains("urgent", StringComparison.Ordinal))
            {
                high += 800;
            }
        }

        var scopeSummary = string.IsNullOrWhiteSpace(features)
            ? (session.CaptureType ?? "your project")
            : $"{session.CaptureType ?? "your project"} with {features}";

        return $"Based on what you’ve shared ({scopeSummary}), a realistic range is ${low:N0}–${high:N0}. Main price drivers are ordering/delivery complexity, content readiness, and timeline. If you want, I can tighten this to a narrower estimate with one final preference (template-led vs custom design).";
    }

    public string BuildAcknowledgementProgressReply(EngageConversationContext ctx)
    {
        var lastAsk = ctx.Session.LastAssistantAskType ?? string.Empty;
        if (lastAsk.Contains("estimate", StringComparison.OrdinalIgnoreCase) || IsPricingIntent(ctx.LastAssistantQuestion ?? string.Empty))
        {
            return BuildScopedEstimate(ctx.Session, ctx.RecentMessages);
        }

        if (lastAsk.Contains("overview", StringComparison.OrdinalIgnoreCase)
            || (ctx.LastAssistantQuestion?.Contains("overview", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            var type = string.IsNullOrWhiteSpace(ctx.Session.CaptureType) ? "business" : ctx.Session.CaptureType;
            return $"Great — here’s a practical overview for your {type}: clear value proposition, trust signals, service/menu highlights, streamlined ordering/contact flow, and mobile-first performance.";
        }

        if (lastAsk.Contains("outline", StringComparison.OrdinalIgnoreCase)
            || (ctx.LastAssistantQuestion?.Contains("outline", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            var features = ExtractFeatureScope(ctx.Session, ctx.RecentMessages);
            return string.IsNullOrWhiteSpace(features)
                ? "Sure — a solid structure is: Home, Services/Menu, About, Ordering/Booking, and Contact."
                : $"Sure — based on your scope, I’d structure it as: {features}.";
        }

        return BuildNaturalNextQuestion(ctx.Session, ctx);
    }

    public string ExtractFeatureScope(EngageChatSession session, IReadOnlyCollection<EngageChatMessage> recentMessages)
    {
        var fromContext = session.CaptureContext ?? string.Empty;
        var recentUser = recentMessages
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Content)
            .TakeLast(4);

        var combined = string.Join(" ", new[] { fromContext }.Concat(recentUser));
        var normalized = Interpreter.NormalizeUserMessage(combined);
        var keywords = new[] { "home page", "services", "menu", "ordering", "delivery", "catering", "contact", "about", "booking" };
        var hits = keywords.Where(k => normalized.Contains(k.Replace(" ", " "), StringComparison.Ordinal)).Distinct().ToArray();
        return string.Join(", ", hits);
    }

    public void MarkConversationCompleted(EngageChatSession session, string reason)
    {
        session.IsConversationComplete = true;
        session.LastCompletedAtUtc = DateTime.UtcNow;
        session.ConversationState = "Discover";
        session.PendingCaptureMode = null;
        session.LastAssistantAskType = reason;
    }

    public void ReopenConversation(EngageChatSession session, string reason)
    {
        session.IsConversationComplete = false;
        session.ConversationState = "Discover";
        session.PendingCaptureMode = null;
        session.LastAssistantAskType = reason;
    }

    private static bool TryInferBusinessSlots(EngageChatSession session, string normalized, string originalMessage)
    {
        var updated = false;

        if (string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            var goalMatch = Regex.Match(normalized, @"\b(?:need|want|looking for|trying to|looking to)\b\s+(.+)");
            if (goalMatch.Success)
            {
                session.CaptureGoal = goalMatch.Groups[1].Value.Trim();
                updated = true;
            }
        }

        if (string.IsNullOrWhiteSpace(session.CaptureType))
        {
            var typeMatch = Regex.Match(normalized, @"\b([a-z0-9\s]{2,40})\s+business\b");
            if (typeMatch.Success)
            {
                session.CaptureType = typeMatch.Groups[1].Value.Trim();
                updated = true;
            }
        }

        if (string.IsNullOrWhiteSpace(session.CaptureLocation))
        {
            var locationMatch = Regex.Match(originalMessage, @"\b(?:in|at|from)\s+([A-Za-z\s,]{2,50})$", RegexOptions.IgnoreCase);
            if (locationMatch.Success)
            {
                session.CaptureLocation = locationMatch.Groups[1].Value.Trim(' ', ',', '.');
                updated = true;
            }
            else if (originalMessage.Trim().Length is > 2 and < 40 && !originalMessage.Contains('@'))
            {
                if (Regex.IsMatch(originalMessage.Trim(), "^[A-Za-z ,'-]+$"))
                {
                    session.CaptureLocation ??= originalMessage.Trim();
                }
            }
        }

        if ((originalMessage.Contains(",", StringComparison.Ordinal) || originalMessage.Contains(" and ", StringComparison.OrdinalIgnoreCase))
            && (normalized.Contains("menu", StringComparison.Ordinal)
                || normalized.Contains("ordering", StringComparison.Ordinal)
                || normalized.Contains("delivery", StringComparison.Ordinal)
                || normalized.Contains("catering", StringComparison.Ordinal)
                || normalized.Contains("contact", StringComparison.Ordinal)))
        {
            session.CaptureContext = string.IsNullOrWhiteSpace(session.CaptureContext)
                ? originalMessage.Trim()
                : $"{session.CaptureContext}; scope: {originalMessage.Trim()}";
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(session.CaptureConstraints)
            && (normalized.Contains("budget", StringComparison.Ordinal)
                || normalized.Contains("timeline", StringComparison.Ordinal)
                || normalized.Contains("week", StringComparison.Ordinal)
                || normalized.Contains("month", StringComparison.Ordinal)
                || normalized.Contains("$", StringComparison.Ordinal)
                || Regex.IsMatch(normalized, @"\b\d+k\b")))
        {
            session.CaptureConstraints = originalMessage.Trim();
            updated = true;
        }

        return updated;
    }
}
