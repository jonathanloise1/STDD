using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Organizations;

/// <summary>
/// The Team screen (`/settings/team`, Admins only): the table of members and the "Edit Team Member" dialog.
/// Tasks tests use the role change; the organizations area uses all of it: the search, the "Invite Team
/// Member" dialog, the "Pending Invitations" table, the role dialog and the "Confirm Removal" dialog.
/// </summary>
public sealed class TeamPage : BasePage
{
    protected override string? PagePath => "/settings/team";

    public TeamPage(IPage page) : base(page) { }

    // ----- Actions -----

    /// <summary>"Click the "Edit" button on the row of ..." with the member's name, for example "Dev Member".</summary>
    public Task ClickEdit(string memberName) =>
        RowFor(memberName).GetByRole(AriaRole.Button, NamedIn("Edit")).ClickAsync();

    /// <summary>"Check the "Viewer" field": the role radio of the dialog, named Admin, Editor or Viewer.</summary>
    public Task CheckRole(string role) =>
        EditDialog.GetByRole(AriaRole.Radio, new() { Name = role, Exact = true }).CheckAsync();

    /// <summary>
    /// "Click the "Save" button" of the dialog. Returns when the dialog has closed, which it does once the
    /// change has been saved: whoever acts next sees the new role.
    /// </summary>
    public async Task ClickSave()
    {
        await EditDialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(EditDialog).ToBeHiddenAsync();
    }


    /// <summary>
    /// "Open the Team page." Returns when the members, or an error, are shown instead of the spinner.
    /// </summary>
    public override async Task Open()
    {
        await base.Open();
        await WaitForMembers();
    }

    /// <summary>
    /// Waits until the page has finished loading the members: the table or an error is shown instead of the
    /// spinner. Needed before saying that something is not listed.
    /// </summary>
    public Task WaitForMembers() =>
        Assertions.Expect(Table.Or(Page.GetByRole(AriaRole.Alert)).First).ToBeVisibleAsync();

    /// <summary>"The "Members" table does not show ...": waits for the table to be loaded, then checks no row has this name.</summary>
    public async Task ExpectNotListed(string memberName)
    {
        await WaitForMembers();
        await Assertions.Expect(RowFor(memberName)).ToHaveCountAsync(0);
    }

    // ----- Actions: search -----

    /// <summary>"Type ... in the "Search team members..." field." Replaces what the field holds.</summary>
    public Task TypeSearch(string text) => TypeAsync(SearchField, text);

    // ----- Actions: the "Invite Team Member" dialog -----

    /// <summary>"Click the "Invite" button."</summary>
    public Task ClickInvite() => Button("Invite").ClickAsync();

    /// <summary>"Type ... in the "Full Name" field."</summary>
    public Task TypeFullName(string text) => TypeAsync(InviteFullNameField, text);

    /// <summary>"Type ... in the "Email" field."</summary>
    public Task TypeInviteEmail(string text) => TypeAsync(InviteEmailField, text);

    /// <summary>"Check the "Editor" field" of the invitation dialog.</summary>
    public Task CheckInviteRole(string role) =>
        InviteDialog.GetByRole(AriaRole.Radio, new() { Name = role, Exact = true }).CheckAsync();

    /// <summary>
    /// "Click the "Send Invitation" button." Returns when sending has finished, whatever the outcome (the
    /// button shows "Sending..." meanwhile).
    /// </summary>
    public async Task ClickSendInvitation()
    {
        await InviteDialog.GetByRole(AriaRole.Button, new() { Name = "Send Invitation", Exact = true }).ClickAsync();
        await Assertions.Expect(InviteDialog.GetByText("Sending...")).ToHaveCountAsync(0);
    }

    // ----- Actions: the role dialog -----

    /// <summary>
    /// "Click the "Save" button" when the dialog must stay open: a change of your own Admin role asks for a
    /// confirmation first, and no request is made. Returns at once.
    /// </summary>
    public Task ClickSaveKeepingTheDialogOpen() => SaveButton.ClickAsync();

    /// <summary>"Click the "Go Back" button" of the demotion confirmation.</summary>
    public Task ClickGoBack() => EditDialog.GetByRole(AriaRole.Button, new() { Name = "Go Back", Exact = true }).ClickAsync();

    /// <summary>"Click the "Confirm Demotion" button." Returns at once; the outcome is a message.</summary>
    public Task ClickConfirmDemotion() =>
        EditDialog.GetByRole(AriaRole.Button, new() { Name = "Confirm Demotion", Exact = true }).ClickAsync();

    /// <summary>"Click the "Cancel" button" of the role dialog.</summary>
    public async Task ClickCancelInRoleDialog()
    {
        await EditDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Assertions.Expect(EditDialog).ToBeHiddenAsync();
    }

    /// <summary>
    /// Gives a member the role if the row does not show it yet, through the same steps a user takes. For
    /// clean-up after a test that changed it; does nothing when the member already has the role.
    /// </summary>
    public async Task EnsureRole(string memberName, string role)
    {
        if (await RoleOf(memberName).InnerTextAsync() == role)
            return;

        await ClickEdit(memberName);
        await CheckRole(role);
        await ClickSave();
    }

    // ----- Actions: the removal -----

    /// <summary>"Click the "Remove" button on the row of ..." Opens the "Confirm Removal" dialog.</summary>
    public Task ClickRemove(string memberName) =>
        RowFor(memberName).GetByRole(AriaRole.Button, NamedIn("Remove")).ClickAsync();

    /// <summary>
    /// "Click the "Remove" button in the dialog." Returns when the dialog has closed, which it does once the
    /// request has finished, whatever the outcome.
    /// </summary>
    public async Task ClickRemoveInDialog()
    {
        await RemoveDialog.GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true }).ClickAsync();
        await Assertions.Expect(RemoveDialog).ToBeHiddenAsync();
    }

    /// <summary>"Click the "Cancel" button in the dialog" of the removal.</summary>
    public Task ClickCancelInRemoveDialog() =>
        RemoveDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

    // ----- State -----

    /// <summary>The title of the page, for example "Team Management - MyApp Dev Org".</summary>
    public ILocator Title(string organizationName) =>
        HeadingWithTail("Team Management", $" - {organizationName}");

    /// <summary>Any text that starts with "Team Management", to say that the page is not shown.</summary>
    public ILocator AnyTitle => Page.GetByText("Team Management");

    /// <summary>The "Search team members..." field.</summary>
    public ILocator SearchField => Field("Search team members...");

    /// <summary>"No team members found": what the table says when the team is empty.</summary>
    public ILocator NoMembersMessage => ExactText("No team members found");

    /// <summary>"No team members match your search".</summary>
    public ILocator NoMatchMessage => ExactText("No team members match your search");

    /// <summary>The "Edit" button on the row of a member (a Viewer-only check uses <see cref="RemoveButtonOf"/>).</summary>
    public ILocator EditButtonOf(string memberName) =>
        RowFor(memberName).GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true });

    /// <summary>The "Remove" button on the row of a member; Admin rows have none.</summary>
    public ILocator RemoveButtonOf(string memberName) =>
        RowFor(memberName).GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true });

    /// <summary>The email cell of a member's row.</summary>
    public ILocator EmailOf(string memberName) => RowFor(memberName).GetByRole(AriaRole.Cell).Nth(1);

    /// <summary>The role cell of a member's row: "Admin", "Editor" or "Viewer".</summary>
    public ILocator RoleOf(string memberName) => RowFor(memberName).GetByRole(AriaRole.Cell).Nth(2);

    /// <summary>The Save button of the role dialog.</summary>
    public ILocator SaveButton => EditDialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true });

    /// <summary>A role radio of the role dialog: "Admin", "Editor" or "Viewer".</summary>
    public ILocator RoleRadio(string role) => EditDialog.GetByRole(AriaRole.Radio, new() { Name = role, Exact = true });

    /// <summary>The warning of the role dialog when you lower your own role.</summary>
    public ILocator DemotionWarning =>
        EditDialog.GetByText("You are removing your own Admin privileges. This action cannot be undone by yourself.");

    /// <summary>A button of the role dialog by name, for example "Go Back" or "Confirm Demotion".</summary>
    public ILocator RoleDialogButton(string name) => EditDialog.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>The "Invite Team Member" dialog.</summary>
    public ILocator InviteDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Invite Team Member" });

    public ILocator InviteFullNameField => InviteDialog.GetByRole(AriaRole.Textbox, new() { Name = "Full Name", Exact = true });

    public ILocator InviteEmailField => InviteDialog.GetByRole(AriaRole.Textbox, new() { Name = "Email", Exact = true });

    /// <summary>The message under a field of the invitation dialog ("Full Name" or "Email").</summary>
    public ILocator InviteFieldMessage(string label) =>
        (label == "Full Name" ? InviteFullNameField : InviteEmailField).Locator("xpath=following-sibling::*[1]");

    /// <summary>A role radio of the invitation dialog: "Admin", "Editor" or "Viewer".</summary>
    public ILocator InviteRoleRadio(string role) => InviteDialog.GetByRole(AriaRole.Radio, new() { Name = role, Exact = true });

    /// <summary>The description shown with a role in the invitation dialog.</summary>
    public ILocator InviteRoleDescription(string role) => InviteDialog.GetByText(role switch
    {
        "Admin" => "Full access to all features, organization settings, team and audit log.",
        "Editor" => "Can create, edit and delete tasks. No access to organization settings.",
        "Viewer" => "Read-only access.",
        _ => throw new ArgumentException($"Unknown role {role}", nameof(role))
    }, new() { Exact = true });

    /// <summary>The "Pending Invitations" table; it is shown only while an invitation is pending.</summary>
    public ILocator PendingTable => Page.GetByRole(AriaRole.Table, new() { Name = "Pending Invitations" });

    /// <summary>The rows of "Pending Invitations" whose name is this text.</summary>
    public ILocator PendingRowFor(string fullName) =>
        PendingTable.Locator("tbody").GetByRole(AriaRole.Row).Filter(new() { Has = Page.GetByText(fullName, new() { Exact = true }) });

    /// <summary>The "Confirm Removal" dialog.</summary>
    public ILocator RemoveDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm Removal" });

    /// <summary>The question of the removal dialog for a member.</summary>
    public ILocator RemoveQuestion(string memberName) =>
        RemoveDialog.GetByText($"Are you sure you want to remove {memberName} from the team? This action cannot be undone.");

    /// <summary>The table of members.</summary>
    public ILocator Table => Page.GetByRole(AriaRole.Table, PageText.IsItalian ? new() { NameRegex = PageText.Exact("Members") } : new() { Name = "Members" });

    /// <summary>The row of a member by name.</summary>
    public ILocator RowFor(string memberName) =>
        Table.Locator("tbody").GetByRole(AriaRole.Row).Filter(new() { Has = Page.GetByText(memberName, new() { Exact = true }) });

    /// <summary>The "Edit Team Member" dialog.</summary>
    public ILocator EditDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Edit Team Member" });
}
