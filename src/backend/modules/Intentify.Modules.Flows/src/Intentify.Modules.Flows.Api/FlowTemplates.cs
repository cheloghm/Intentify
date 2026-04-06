namespace Intentify.Modules.Flows.Api;

internal static class FlowTemplates
{
    public static readonly IReadOnlyCollection<FlowTemplateDto> All =
    [
        new FlowTemplateDto(
            Id: "notify-team-new-lead",
            Name: "Notify team on new lead",
            Description: "Send a team notification whenever a new lead is captured via Engage chat.",
            Trigger: new FlowTemplateTriggerDto("engage_lead_captured", null),
            Conditions: [],
            Actions:
            [
                new FlowTemplateActionDto("NotifyTeam", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["message"] = "New lead captured: {{email}}"
                })
            ]),

        new FlowTemplateDto(
            Id: "webhook-ticket-created",
            Name: "Send webhook on ticket created",
            Description: "Fire a webhook to your endpoint whenever a support ticket is created.",
            Trigger: new FlowTemplateTriggerDto("engage_ticket_created", null),
            Conditions: [],
            Actions:
            [
                new FlowTemplateActionDto("SendWebhook", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["url"] = ""
                })
            ]),

        new FlowTemplateDto(
            Id: "tag-lead-deciding-on-return",
            Name: "Tag lead as deciding on return visit",
            Description: "Automatically update a lead's stage to 'deciding' when they return to the site.",
            Trigger: new FlowTemplateTriggerDto("visitor_return", null),
            Conditions: [],
            Actions:
            [
                new FlowTemplateActionDto("UpdateLeadStage", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["email"] = "",
                    ["label"] = "deciding"
                })
            ]),

        new FlowTemplateDto(
            Id: "exit-intent-slack-alert",
            Name: "Exit intent alert",
            Description: "Send a Slack message when a visitor shows exit intent behaviour.",
            Trigger: new FlowTemplateTriggerDto("exit_intent", null),
            Conditions: [],
            Actions:
            [
                new FlowTemplateActionDto("SendSlackNotification", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["webhookUrl"] = "",
                    ["message"] = "Visitor showing exit intent on {{pageUrl}}"
                })
            ]),

        new FlowTemplateDto(
            Id: "log-all-conversations",
            Name: "Log all conversations",
            Description: "Log every completed conversation for auditing and reporting purposes.",
            Trigger: new FlowTemplateTriggerDto("engage_conversation_completed", null),
            Conditions: [],
            Actions:
            [
                new FlowTemplateActionDto("LogRun", null)
            ])
    ];
}

internal sealed record FlowTemplateDto(
    string Id,
    string Name,
    string Description,
    FlowTemplateTriggerDto Trigger,
    IReadOnlyCollection<object> Conditions,
    IReadOnlyCollection<FlowTemplateActionDto> Actions);

internal sealed record FlowTemplateTriggerDto(string TriggerType, IReadOnlyDictionary<string, string>? Filters);

internal sealed record FlowTemplateActionDto(string ActionType, IReadOnlyDictionary<string, string>? Params);
