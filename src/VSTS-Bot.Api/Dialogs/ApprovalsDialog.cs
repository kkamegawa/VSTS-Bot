﻿// ———————————————————————————————
// <copyright file="ApprovalsDialog.cs">
// Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
// <summary>
// Represents the dialog to retrieve and approve/reject approvals.
// </summary>
// ———————————————————————————————

namespace Vsar.TSBot.Dialogs
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Cards;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;
    using Resources;

    /// <summary>
    /// Represents the dialog to retrieve and approve/reject approvals.
    /// </summary>
    [CommandMetadata("approvals")]
    [Serializable]
    public class ApprovalsDialog : DialogBase, IDialog<object>
    {
        private const int TakeSize = 7;

        private const string CommandMatchApprovals = "approvals";
        private const string CommandMatchApprove = @"approve (\d+) *(.*?)$";
        private const string CommandMatchReject = @"reject (\d+) *(.*?)$";

        /// <summary>
        /// Initializes a new instance of the <see cref="ApprovalsDialog"/> class.
        /// </summary>
        /// <param name="vstsService">The <see cref="IVstsService"/>.</param>
        /// <param name="applicationRegistry">The <see cref="IVstsApplicationRegistry"/>.</param>
        public ApprovalsDialog(IVstsService vstsService, IVstsApplicationRegistry applicationRegistry)
            : base(vstsService, applicationRegistry)
        {
        }

        /// <summary>
        /// Gets or sets the account.
        /// </summary>
        public string Account { get; set; }

        /// <summary>
        /// Gets or sets the approvalid.
        /// </summary>
        public int ApprovalId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether it is an approval.
        /// </summary>
        public bool IsApproved { get; set; }

        /// <summary>
        /// Gets or sets the profile.
        /// </summary>
        public VstsProfile Profile { get; set; }

        /// <summary>
        /// Gets or sets the Team Project.
        /// </summary>
        public string TeamProject { get; set; }

        /// <inheritdoc />
        public async Task StartAsync(IDialogContext context)
        {
            context.ThrowIfNull(nameof(context));

            context.Wait(this.ApprovalsAsync);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Replies with a list of ApprovalCards.
        /// </summary>
        /// <param name="context">The <see cref="IDialogContext"/>.</param>
        /// <param name="result">The <see cref="IAwaitable{T}"/>.</param>
        /// <returns>An async <see cref="Task"/>/.</returns>
        public virtual async Task ApprovalsAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            context.ThrowIfNull(nameof(context));
            result.ThrowIfNull(nameof(result));

            var activity = await result;

            this.Account = context.UserData.GetAccount();
            this.Profile = context.UserData.GetProfile(this.GetAuthenticationService(activity));
            this.TeamProject = context.UserData.GetTeamProject();

            var text = (activity.Text ?? string.Empty).Trim().ToLowerInvariant();

            if (text.Equals(CommandMatchApprovals, StringComparison.OrdinalIgnoreCase))
            {
                var approvals = await this.VstsService.GetApprovals(this.Account, this.TeamProject, this.Profile);
                if (!approvals.Any())
                {
                    var reply = context.MakeMessage();
                    reply.Text = Labels.NoApprovals;
                    await context.PostAsync(reply);

                    context.Done(reply);
                    return;
                }

                var skip = 0;
                while (skip < approvals.Count)
                {
                    var cards = approvals.Skip(skip).Take(TakeSize).Select(a => new ApprovalCard(this.Account, a, this.TeamProject)).ToList();
                    var reply = context.MakeMessage();

                    foreach (var card in cards)
                    {
                        reply.Attachments.Add(card);
                    }

                    reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                    await context.PostAsync(reply);

                    skip += TakeSize;
                }

                context.Wait(this.ApproveOrRejectAsync);
            }
            else
            {
                context.Fail(new UnknownCommandException(activity.Text));
            }
        }

        /// <summary>
        /// Approves or Rejects an Approval.
        /// </summary>
        /// <param name="context">The <see cref="IDialogContext"/>.</param>
        /// <param name="result">The <see cref="IAwaitable{T}"/>.</param>
        /// <returns>An async <see cref="Task"/>/.</returns>
        public virtual async Task ApproveOrRejectAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            context.ThrowIfNull(nameof(context));
            result.ThrowIfNull(nameof(result));

            var activity = await result;

            var matchApprove = Regex.Match(activity.RemoveRecipientMention(), CommandMatchApprove);
            var matchReject = Regex.Match(activity.RemoveRecipientMention(), CommandMatchReject);

            var reply = context.MakeMessage();

            if (matchApprove.Success)
            {
                this.ApprovalId = Convert.ToInt32(matchApprove.Groups[1].Value);
                this.IsApproved = true;
                var comment = matchApprove.Groups[2].Value;

                if (string.IsNullOrWhiteSpace(comment))
                {
                    reply.Text = Labels.MissingComment;
                    await context.PostAsync(reply);
                    context.Wait(this.ChangeStatusAsync);
                }
                else
                {
                    await this.ChangeStatusAsync(context, this.ApprovalId, comment, true);
                }
            }
            else if (matchReject.Success)
            {
                this.ApprovalId = Convert.ToInt32(matchReject.Groups[1].Value);
                this.IsApproved = false;
                var comment = matchReject.Groups[2].Value;

                if (string.IsNullOrWhiteSpace(comment))
                {
                    reply.Text = Labels.MissingComment;
                    await context.PostAsync(reply);
                    context.Wait(this.ChangeStatusAsync);
                }
                else
                {
                    await this.ChangeStatusAsync(context, this.ApprovalId, comment, false);
                }
            }
            else
            {
                context.Fail(new UnknownCommandException(activity.Text));
            }
        }

        /// <summary>
        /// Changes the status of an Approval.
        /// </summary>
        /// <param name="context">A <see cref="IDialogContext"/>.</param>
        /// <param name="result">A <see cref="IMessageActivity"/>.</param>
        /// <returns>A <see cref="Task"/>.</returns>
        public virtual async Task ChangeStatusAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            context.ThrowIfNull(nameof(context));
            result.ThrowIfNull(nameof(result));

            var activity = await result;

            await this.ChangeStatusAsync(context, this.ApprovalId, activity.RemoveRecipientMention().Trim(), this.IsApproved);
        }

        /// <summary>
        /// Changes the status of an Approval.
        /// </summary>
        /// <param name="context">A <see cref="IDialogContext"/>.</param>
        /// <param name="approvalId">The approval id.</param>
        /// <param name="comment">A comment.</param>
        /// <param name="isApproved">Indication if it is approved.</param>
        /// <returns>A <see cref="Task"/>.</returns>
        public virtual async Task ChangeStatusAsync(IDialogContext context, int approvalId, string comment, bool isApproved)
        {
            context.ThrowIfNull(nameof(context));

            var reply = context.MakeMessage();

            var status = isApproved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
            await this.VstsService.ChangeApprovalStatus(this.Account, this.TeamProject, this.Profile, approvalId, status, comment);

            reply.Text = isApproved ? Labels.Approved : Labels.Rejected;
            await context.PostAsync(reply);

            context.Done(reply);
        }
    }
}