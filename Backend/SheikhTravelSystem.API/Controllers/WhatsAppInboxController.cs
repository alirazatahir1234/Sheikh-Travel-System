using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[Route("api/whatsapp")]
public class WhatsAppInboxController : BaseApiController
{
    [HttpGet("accounts")]
    [RequirePermission(WhatsAppPermissions.View)]
    public async Task<IActionResult> GetAccounts([FromQuery] bool includeInactive = false)
        => Ok(await Mediator.Send(new GetWhatsAppAccountsQuery(includeInactive)));

    [HttpPatch("accounts/{id:int}/active")]
    [RequirePermission(WhatsAppPermissions.ManageAccounts)]
    public async Task<IActionResult> SetAccountActive(int id, [FromBody] SetWhatsAppAccountActiveRequest body)
        => Ok(await Mediator.Send(new SetWhatsAppAccountActiveCommand(id, body.IsActive)));

    [HttpPost("accounts/{id:int}/health")]
    [RequirePermission(WhatsAppPermissions.ManageAccounts)]
    public async Task<IActionResult> CheckAccountHealth(int id)
        => Ok(await Mediator.Send(new CheckWhatsAppAccountHealthCommand(id)));

    [HttpGet("templates")]
    [RequirePermission(WhatsAppPermissions.View)]
    public async Task<IActionResult> GetTemplates([FromQuery] int? accountId, [FromQuery] string? status)
        => Ok(await Mediator.Send(new GetWhatsAppTemplatesQuery(accountId, status)));

    [HttpPost("templates")]
    [RequirePermission(WhatsAppPermissions.ManageTemplates)]
    public async Task<IActionResult> UpsertTemplate([FromBody] UpsertWhatsAppTemplateRequest body)
        => Ok(await Mediator.Send(new UpsertWhatsAppTemplateCommand(
            body.WhatsAppAccountId,
            body.Name ?? "",
            body.Language ?? "en",
            body.Category ?? "UTILITY",
            body.Status ?? "Draft",
            body.MetaTemplateId,
            body.BodyPreview)));

    [HttpPatch("templates/{id:int}/status")]
    [RequirePermission(WhatsAppPermissions.ManageTemplates)]
    public async Task<IActionResult> SetTemplateStatus(int id, [FromBody] SetWhatsAppTemplateStatusRequest body)
        => Ok(await Mediator.Send(new SetWhatsAppTemplateStatusCommand(id, body.Status ?? "", body.MetaTemplateId)));

    [HttpPost("send-template")]
    [RequirePermission(WhatsAppPermissions.Reply)]
    public async Task<IActionResult> SendTemplate([FromBody] SendWhatsAppTemplateRequest body)
        => Ok(await Mediator.Send(new SendWhatsAppTemplateCommand(
            body.ConversationId,
            body.WhatsAppAccountId,
            body.RecipientPhoneNumber,
            body.TemplateName ?? "",
            body.Language ?? "en",
            body.BodyParameters)));

    [HttpGet("conversations")]
    [RequirePermission(WhatsAppPermissions.View)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int? accountId,
        [FromQuery] string? search,
        [FromQuery] string? filter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
        => Ok(await Mediator.Send(new GetWhatsAppConversationsQuery(accountId, search, filter, page, pageSize)));

    [HttpPatch("conversations/{id:int}/assignment")]
    [RequirePermission(WhatsAppPermissions.Reply)]
    public async Task<IActionResult> SetAssignment(int id, [FromBody] SetWhatsAppAssignmentRequest body)
        => Ok(await Mediator.Send(new SetWhatsAppConversationAssignmentCommand(id, body.AssignedUserId)));

    [HttpPatch("conversations/{id:int}/status")]
    [RequirePermission(WhatsAppPermissions.Reply)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetWhatsAppStatusRequest body)
        => Ok(await Mediator.Send(new SetWhatsAppConversationStatusCommand(id, body.Status ?? "")));

    [HttpPatch("conversations/{id:int}/bot")]
    [RequirePermission(WhatsAppPermissions.Reply)]
    public async Task<IActionResult> SetBot(int id, [FromBody] SetWhatsAppBotRequest body)
        => Ok(await Mediator.Send(new SetWhatsAppConversationBotCommand(id, body.IsBotEnabled, body.CurrentBotState)));

    [HttpGet("conversations/{id:int}/messages")]
    [RequirePermission(WhatsAppPermissions.View)]
    public async Task<IActionResult> GetMessages(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => Ok(await Mediator.Send(new GetWhatsAppMessagesQuery(id, page, pageSize)));

    [HttpPost("conversations/{id:int}/messages")]
    [RequirePermission(WhatsAppPermissions.Reply)]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendWhatsAppMessageRequest body)
        => Ok(await Mediator.Send(new SendWhatsAppMessageCommand(id, body.Body ?? "")));

    /// <summary>Multi-number outbound send. Frontend must not send PhoneNumberId or access tokens.</summary>
    [HttpPost("send")]
    [RequirePermission(WhatsAppPermissions.Reply)]
    public async Task<IActionResult> SendOutbound([FromBody] SendWhatsAppOutboundRequest body)
        => Ok(await Mediator.Send(new SendWhatsAppOutboundCommand(
            body.WhatsAppAccountId,
            body.ConversationId,
            body.RecipientPhoneNumber,
            body.MessageType ?? "text",
            body.Text)));

    [HttpPost("conversations/{id:int}/read")]
    [RequirePermission(WhatsAppPermissions.View)]
    public async Task<IActionResult> MarkRead(int id)
        => Ok(await Mediator.Send(new MarkWhatsAppConversationReadCommand(id)));

    [HttpGet("contacts/{id:int}")]
    [RequirePermission(WhatsAppPermissions.View)]
    public async Task<IActionResult> GetContact(int id)
        => Ok(await Mediator.Send(new GetWhatsAppContactQuery(id)));

    [HttpPost("contacts/{id:int}/link-customer")]
    [RequirePermission(WhatsAppPermissions.Manage)]
    public async Task<IActionResult> LinkCustomer(int id, [FromBody] LinkWhatsAppCustomerRequest body)
        => Ok(await Mediator.Send(new LinkWhatsAppContactCustomerCommand(id, body.CustomerId)));

    [HttpPost("contacts/{id:int}/auto-link")]
    [RequirePermission(WhatsAppPermissions.Manage)]
    public async Task<IActionResult> AutoLink(int id)
        => Ok(await Mediator.Send(new AutoLinkWhatsAppContactCommand(id)));

    [HttpGet("media")]
    [RequirePermission(WhatsAppPermissions.View)]
    public async Task<IActionResult> GetMedia([FromQuery] string mediaId, [FromQuery] int accountId)
    {
        var result = await Mediator.Send(new GetWhatsAppMediaQuery(mediaId, accountId));
        if (!result.Success || result.Data is null)
            return NotFound(result);
        return File(result.Data.Bytes, result.Data.ContentType);
    }
}

public record SendWhatsAppMessageRequest(string? Body);
public record SendWhatsAppOutboundRequest(
    int? WhatsAppAccountId,
    int? ConversationId,
    string? RecipientPhoneNumber,
    string? MessageType,
    string? Text);
public record SendWhatsAppTemplateRequest(
    int? ConversationId,
    int? WhatsAppAccountId,
    string? RecipientPhoneNumber,
    string? TemplateName,
    string? Language,
    IReadOnlyList<string>? BodyParameters);
public record UpsertWhatsAppTemplateRequest(
    int WhatsAppAccountId,
    string? Name,
    string? Language,
    string? Category,
    string? Status,
    string? MetaTemplateId,
    string? BodyPreview);
public record SetWhatsAppTemplateStatusRequest(string? Status, string? MetaTemplateId = null);
public record SetWhatsAppAccountActiveRequest(bool IsActive);
public record SetWhatsAppAssignmentRequest(int? AssignedUserId);
public record SetWhatsAppStatusRequest(string? Status);
public record SetWhatsAppBotRequest(bool IsBotEnabled, string? CurrentBotState = null);
public record LinkWhatsAppCustomerRequest(int? CustomerId);
