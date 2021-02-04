﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using StormBot.Services;
using StormBot.Database.Entities;

namespace StormBot.Modules.CallOfDutyModules
{
	public class BaseCallOfDutyCommands : BaseCommand
	{
        public async Task UnassignRoleFromAllMembers(ulong roleID, SocketGuild guild)
        {
            var role = guild.GetRole(roleID);
            IEnumerable<SocketGuildUser> roleMembers = guild.GetRole(roleID).Members;
            foreach (SocketGuildUser roleMember in roleMembers)
            {
                await roleMember.RemoveRoleAsync(role);
            }
        }

        public async Task GiveUsersRole(ulong roleID, List<ulong> discordIDs, SocketGuild guild)
        {
            var role = guild.GetRole(roleID);

            foreach (ulong discordID in discordIDs)
            {
                var roleMember = guild.GetUser(discordID);
                await roleMember.AddRoleAsync(role);
            }
        }

        public async Task<bool> AddAParticipant(CallOfDutyService service, ulong serverID, ulong discordID, string gameAbbrev, string modeAbbrev)
        {
            CallOfDutyPlayerDataEntity newAccount = new CallOfDutyPlayerDataEntity();

            newAccount.ServerID = serverID;
            newAccount.DiscordID = discordID;
            newAccount.GameAbbrev = gameAbbrev;
            newAccount.ModeAbbrev = modeAbbrev;

            await Context.User.SendMessageAsync(string.Format("What is <@!{0}>'s Call of Duty username? Capitalization matters. Do not include the '#number' tag after the name. (on Battle.net, PlayStation, Xbox, Steam, Activision)", discordID));
            newAccount.Username = await PromptUserForStringForPartcipant(service);

            if (newAccount.Username == "cancel")
                return false;

            await Context.User.SendMessageAsync(string.Format("What is <@!{0}>'s Call of Duty username's tag? If there is no tag, say 'none'. Do not include the '#' symbol in your answer. (Example: 1234 in User#1234)", discordID));
            newAccount.Tag = await PromptUserForStringForPartcipant(service, true);

            if (newAccount.Tag == "cancel")
                return false;

            newAccount.Platform = await AskPlatform(service, discordID);

            if (newAccount.Platform == "cancel")
                return false;

            service.AddParticipantToDatabase(newAccount);
            
            return true;
        }

        public async Task<bool> RemoveAParticipant(CallOfDutyService service, ulong serverID, ulong discordID, string gameAbbrev, string modeAbbrev)
        {
            CallOfDutyPlayerDataEntity removeAccount = await GetCallOfDutyPlayerDataEntity(service, serverID, discordID, gameAbbrev, modeAbbrev);

            if (removeAccount != null)
            {
                service.RemoveParticipantFromDatabase(removeAccount);

                return true;
            }
            else
            {
                await ReplyAsync("This user isn't participating.");
                return false;
            }
        }

        public async Task<string> PromptUserForStringForPartcipant(CallOfDutyService service, bool forTag = false)
        {
            var userSelectResponse = await NextMessageAsync(true, false, new TimeSpan(0, 1, 0));

            string requestedString = null;

            // if user responds in time
            if (userSelectResponse != null)
            {
                requestedString = userSelectResponse.Content;

                if (forTag && requestedString == "none")
                {
                    return "";
                }
                // if response is cancel, don't add participant
                if (requestedString.ToLower() == "cancel")
                {
                    await Context.User.SendMessageAsync("Request cancelled.");
                    return "cancel";
                }
                // if same user starts another command while awaiting a response, end this one but don't display request cancelled
                else if (requestedString.StartsWith(await GetServerPrefix(service._db)))
                {
                    return "cancel";
                }
            }
            // if user doesn't respond in time
            else
            {
                await Context.User.SendMessageAsync("You did not reply before the timeout.");
                return "cancel";
            }

            return requestedString;
        }

        public async Task<string> AskPlatform(CallOfDutyService service, ulong discordID)
        {
            string platforms = "**1.)** Battle.net\n**2.)** PlayStation\n**3.)** Xbox\n**4.)** Steam\n**5.)** Activision\n";

            await Context.User.SendMessageAsync(string.Format("What is <@!{0}>'s Call of Duty username's game platform? Please respond with the corresponding number:\n", discordID) + platforms);
            int selection = await PromptUserForNumber(service, 5);

            switch (selection)
            {
                case (-1):
                    return "cancel";
                case (1):
                    return "battle";
                case (2):
                    return "psn";
                case (3):
                    return "xbl";
                case (4):
                    return "steam";
                default:
                    return "uno";
            }
        }

        public async Task<int> PromptUserForNumber(CallOfDutyService service, int maxSelection)
        {
            var userSelectResponse = await NextMessageAsync(true, false, new TimeSpan(0, 1, 0));

            string username = Context.User.Username;

            // if user responds in time
            if (userSelectResponse != null)
            {
                string requestedNumber = userSelectResponse.Content;

                // if response is not a number
                if (!(int.TryParse(requestedNumber, out int validatedNumber)))
                {
                    // if response is cancel, don't remove
                    if (requestedNumber.ToLower() == "cancel")
                    {
                        await Context.User.SendMessageAsync("Request cancelled.");
                        return -1;
                    }
                    // if same user starts another command while awaiting a response, end this one but don't display request cancelled
                    else if (requestedNumber.StartsWith(await GetServerPrefix(service._db)))
                    {
                        return -1;
                    }
                    // if not cancel, request another response
                    else
                    {
                        await Context.User.SendMessageAsync($"{username}, your response was invalid. Please answer with a number.");
                        return await PromptUserForNumber(service, maxSelection);
                    }
                }
                // if response is a number
                else
                {
                    // if number is valid option on list of sounds
                    if (validatedNumber >= 1 && validatedNumber <= maxSelection)
                    {
                        await Context.User.SendMessageAsync($"{username} entered: {validatedNumber}");
                        return validatedNumber;
                    }
                    // if not valid number, request another response
                    else
                    {
                        await Context.User.SendMessageAsync($"{username}, your response was invalid. Please answer a number shown on the list.");
                        return await PromptUserForNumber(service, maxSelection);
                    }
                }
            }
            // if user doesn't respond in time
            else
            {
                await Context.User.SendMessageAsync("You did not reply before the timeout.");
                return -1;
            }
        }

        public async Task<List<CallOfDutyPlayerDataEntity>> ListPartcipants(CallOfDutyService service, ulong serverId, string gameAbbrev, string modeAbbrev)
        {
            List<ulong> serverIdList = new List<ulong>();
            serverIdList.Add(serverId);

            List<CallOfDutyPlayerDataEntity> participatingAccountsData = await service.GetServersPlayerData(serverIdList, gameAbbrev, modeAbbrev);

            string gameName = "";
            if (gameAbbrev == "mw" && modeAbbrev == "mp")
                gameName = "Modern Warfare";
            else if (gameAbbrev == "mw" && modeAbbrev == "wz")
                gameName = "Warzone";
            else if (gameAbbrev == "cw" && modeAbbrev == "mp")
                gameName = "Black Ops Cold War";

            string output = "__**Participants: " + gameName + "**__\n";

            if (participatingAccountsData.Count != 0)
            {
                int accountCount = 1;
                foreach (CallOfDutyPlayerDataEntity account in participatingAccountsData)
                {
                    ulong discordID = account.DiscordID;
                    string username = account.Username;
                    string tag = "";
                    string platform = "";

                    if (account.Tag != "")
                        tag = "#" + account.Tag;

                    if (account.Platform == "battle")
                        platform = "Battle.net";
                    else if (account.Platform == "steam")
                        platform = "Steam";
                    else if (account.Platform == "psn")
                        platform = "PlayStation";
                    else if (account.Platform == "xbl")
                        platform = "Xbox";
                    else if (account.Platform == "uno")
                        platform = "Activision";

                    output += string.Format(@"**{0}.)** <@!{1}> ({2}{3}, {4}).", accountCount, discordID, username, tag, platform) + "\n";

                    accountCount++;
                }
                await ReplyAsync(output);
            }
            else
            {
                await ReplyAsync("Zero participants.");
            }
            return participatingAccountsData;
        }

        public async Task<CallOfDutyPlayerDataEntity> GetCallOfDutyPlayerDataEntity(CallOfDutyService service, ulong serverID, ulong discordID, string gameAbbrev, string modeAbbrev)
        {
            return await service._db.CallOfDutyPlayerData
                .AsQueryable()
                .Where(player => player.ServerID == serverID && player.DiscordID == discordID && player.GameAbbrev == gameAbbrev && player.ModeAbbrev == modeAbbrev)
                .SingleOrDefaultAsync();
        }

        public async Task<bool> MissedLastDataFetch(CallOfDutyService service, ulong serverID, ulong discordID, string gameAbbrev, string modeAbbrev)
        {
            CallOfDutyPlayerDataEntity data = await GetCallOfDutyPlayerDataEntity(service, serverID, discordID, gameAbbrev, modeAbbrev);

            DateTime lastDataFetchDay = DateTime.Now;

            // if today is Sunday, the last data fetch was this morning
            // if today is not Sunday, the last data fetch was the last Sunday
            while (lastDataFetchDay.DayOfWeek != DayOfWeek.Sunday)
                lastDataFetchDay = lastDataFetchDay.AddDays(-1);

            if (data.Date.Date == lastDataFetchDay.Date)
                return false;
            else
                return true;
        }
    }
}
