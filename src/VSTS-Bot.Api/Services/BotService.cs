﻿// ———————————————————————————————
// <copyright file="BotService.cs">
// Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
// <summary>
// Represents an implementation for IBotService.
// </summary>
// ———————————————————————————————

namespace Vsar.TSBot
{
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Microsoft.Bot.Connector;

    /// <summary>
    /// Represents an implementation for IBotService.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class BotService : IBotService
    {
        private readonly IBotState botState;

        /// <summary>
        /// Initializes a new instance of the <see cref="BotService"/> class.
        /// </summary>
        /// <param name="botState">The botstate.</param>
        public BotService(IBotState botState)
        {
            this.botState = botState;
        }

        /// <inheritdoc />
        public async Task<BotData> GetUserData(string channelId, string userId)
        {
            return await this.botState.GetUserDataAsync(channelId, userId);
        }

        /// <inheritdoc />
        public async Task SetUserData(string channelId, string userId, BotData botData)
        {
            await this.botState.SetUserDataAsync(channelId, userId, botData);
        }
    }
}