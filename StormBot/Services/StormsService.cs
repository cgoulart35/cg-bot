﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using StormBot.Database;
using StormBot.Database.Entities;
using Discord.Commands;

namespace StormBot.Services
{
	public class StormsService : BaseService
	{
		public readonly DiscordSocketClient _client;

		private Dictionary<ulong, int> OngoingStormsLevel;
		private Dictionary<ulong, int> OngoingStormsWinningNumber;
		private Dictionary<ulong, Dictionary<ulong, int>> OngoingStormsUserGuessCount;
		private Dictionary<ulong, List<ulong>> OngoingStormsUsersWaitingForStealTimeLimit;

		private Random random;

		public Emoji cloud_rain;
		public Emoji thunder_cloud_rain;
		public Emoji umbrella2;
		public Emoji white_sun_rain_cloud;
		public Emoji sun_with_face;
		public Emoji rotating_light;

		public double levelOneReward = 10;
		public double levelTwoReward = 50;
		public double resetBalance = 10;
		public double resetMark = 50000;
		public double disasterMark = 2000;
		public double insuranceCost = 1000;
		public double stealAmount = 5;
		public int stealTimeLimitInSeconds = 10;

		public List<IUserMessage> purgeCollection;

		public StormsService(IServiceProvider services)
		{
			_client = services.GetRequiredService<DiscordSocketClient>();
			_db = services.GetRequiredService<StormBotContext>();

			Name = "Storm Service";
			isServiceRunning = false;

			OngoingStormsLevel = new Dictionary<ulong, int>();
			OngoingStormsWinningNumber = new Dictionary<ulong, int>();
			OngoingStormsUserGuessCount = new Dictionary<ulong, Dictionary<ulong, int>>();
			OngoingStormsUsersWaitingForStealTimeLimit = new Dictionary<ulong, List<ulong>>();

			random = new Random();

			cloud_rain = new Emoji("🌧️");
			thunder_cloud_rain = new Emoji("⛈️");
			umbrella2 = new Emoji("☂️");
			white_sun_rain_cloud = new Emoji("🌦️");
			sun_with_face = new Emoji("🌞");
			rotating_light = new Emoji("🚨");

			purgeCollection = new List<IUserMessage>();
	}

		public override async Task StartService()
		{
			string logStamp = GetLogStamp();

			if (!DoStart)
			{
				Console.WriteLine(logStamp + "Disabled.".PadLeft(60 - logStamp.Length));
			}
			else if (isServiceRunning)
			{
				Console.WriteLine(logStamp + "Service already running.".PadLeft(75 - logStamp.Length));
			}
			else
			{
				Console.WriteLine(logStamp + "Starting service.".PadLeft(68 - logStamp.Length));

				isServiceRunning = true;

				List<ServersEntity> servers = await GetAllServerEntities();
				foreach (ServersEntity server in servers)
				{
					string message = "";

					if (isServiceRunning && server.AllowServerPermissionStorms && server.ToggleStorms)
					{
						message += "_**[    STORMS ONLINE.    ]**_\n";
					}

					if (server.StormsNotificationChannelID != 0 && message != "")
						await ((IMessageChannel)_client.GetChannel(server.StormsNotificationChannelID)).SendMessageAsync(message);
				}
			}
		}

		public override async Task StopService()
		{
			string logStamp = GetLogStamp();

			if (isServiceRunning)
			{
				Console.WriteLine(logStamp + "Stopping service.".PadLeft(68 - logStamp.Length));

				List<ServersEntity> servers = await GetAllServerEntities();
				foreach (ServersEntity server in servers)
				{
					string message = "";

					if (isServiceRunning && server.AllowServerPermissionStorms && server.ToggleStorms)
					{
						message += "_**[    STORMS OFFLINE.    ]**_\n";
					}

					if (server.StormsNotificationChannelID != 0 && message != "")
						await ((IMessageChannel)_client.GetChannel(server.StormsNotificationChannelID)).SendMessageAsync(message);
				}

				isServiceRunning = false;
			}
		}

		public async Task HandleIncomingStorm(object sender, ulong serverId, string serverName, ulong channelId)
		{
			string logStamp = GetLogStamp();

			int randomNumber = random.Next(1, 201);
			Console.WriteLine(logStamp + $"				The winning number for the ongoing Storm in {serverName} is {randomNumber}.");

			OngoingStormsLevel.Add(channelId, 1);
			OngoingStormsWinningNumber.Add(channelId, randomNumber);

			Dictionary<ulong, int> UserGuessCountsInServer = new Dictionary<ulong, int>();
			OngoingStormsUserGuessCount.Add(channelId, UserGuessCountsInServer);

			List<ulong> UsersWaitingInServerForSteal = new List<ulong>();
			OngoingStormsUsersWaitingForStealTimeLimit.Add(channelId, UsersWaitingInServerForSteal);

			purgeCollection.Add(await((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync(cloud_rain.ToString() + thunder_cloud_rain.ToString() + umbrella2.ToString() + " __**STORM INCOMING**__ " + umbrella2.ToString() + thunder_cloud_rain.ToString() + cloud_rain.ToString() + string.Format(@"

First to use '**{0}umbrella**' starts the Storm and earns {1} points! 10 minute countdown starting now!", await GetServerPrefix(serverId), levelOneReward)));

			StartStormCountdown(channelId);
		}

		public async Task EndStorm(ulong channelId)
		{
			bool wasRemoved = OngoingStormsLevel.Remove(channelId);
			OngoingStormsWinningNumber.Remove(channelId);
			OngoingStormsUserGuessCount.Remove(channelId);
			OngoingStormsUsersWaitingForStealTimeLimit.Remove(channelId);

			if (wasRemoved)
			{
				purgeCollection.Add(await ((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync(sun_with_face.ToString() + sun_with_face.ToString() + sun_with_face.ToString() + " __**STORM OVER**__ " + sun_with_face.ToString() + sun_with_face.ToString() + sun_with_face.ToString()));

				// wait 1 minute
				await Task.Delay(60 * 1000);

				// delete all messages added to purge collection
				await ((ITextChannel)_client.GetChannel(channelId)).DeleteMessagesAsync(purgeCollection);
			}
		}

		public async Task StartStormCountdown(ulong channelId)
		{
			int actualLevel;

			// end the storm in 10 minutes

			// wait 5 minutes
			await Task.Delay(300 * 1000);

			// announce 5 minutes left if still ongoing
			if (OngoingStormsLevel.TryGetValue(channelId, out actualLevel))
				purgeCollection.Add(await((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync(white_sun_rain_cloud.ToString() + white_sun_rain_cloud.ToString() + white_sun_rain_cloud.ToString() + " __**5 MINUTES REMAINING!**__ " + white_sun_rain_cloud.ToString() + white_sun_rain_cloud.ToString() + white_sun_rain_cloud.ToString()));

			// wait 4 minutes
			await Task.Delay(240 * 1000);

			// announce 1 minute left if still ongoing
			if (OngoingStormsLevel.TryGetValue(channelId, out actualLevel))
				purgeCollection.Add(await((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync(white_sun_rain_cloud.ToString() + white_sun_rain_cloud.ToString() + white_sun_rain_cloud.ToString() + " __**1 MINUTE REMAINING!**__ " + white_sun_rain_cloud.ToString() + white_sun_rain_cloud.ToString() + white_sun_rain_cloud.ToString()));

			// wait 1 minute
			await Task.Delay(60 * 1000);

			if (OngoingStormsLevel.TryGetValue(channelId, out actualLevel))
				await EndStorm(channelId);
		}

		public async Task StartUsersStealTimeLimitCountdown(ulong channelId, ulong discordId)
		{
			// wait x seconds before removal from waitlist
			await Task.Delay(stealTimeLimitInSeconds * 1000);

			OngoingStormsUsersWaitingForStealTimeLimit[channelId].Remove(discordId);
		}

		public async Task TryToUpdateOngoingStorm(SocketGuild guild, ulong serverId, ulong discordId, ulong channelId, int inputLevel, int? guess = null, double? bet = null)
		{
			int actualLevel;

			// if there is an ongoing storm
			if (OngoingStormsLevel.TryGetValue(channelId, out actualLevel))
			{
				// if the ongoing storm is on the correct step for this command, give the user points and update the storm level
				if (actualLevel == inputLevel)
				{
					StormPlayerDataEntity playerData = await GetStormPlayerDataEntity(serverId, discordId);
					bool hadDisasterMark = playerData.Wallet >= disasterMark;

					if (actualLevel == 1)
					{
						// give user points for level 1
						playerData.Wallet += levelOneReward;
						await _db.SaveChangesAsync();

						purgeCollection.Add(await ((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync($"<@!{discordId}>, you put up your umbrella first and earned {levelOneReward} points!" + string.Format(@"

__**First to guess the winning number correctly between 1 and 200 earns points!**__
Use '**{0}guess [number]**' to make a guess with a winning reward of {1} points!
Use '**{0}bet [points] [number]**' to make a guess. If you win, you earn the amount of points bet within your wallet. If you lose, you lose those points.
Use '**{0}steal**' to steal {2} points from the player with the most points.

Use '**{0}buy insurance**' to buy insurance for {3} points to protect your wallet from disasters.
Use '**{0}wallet**' to show how many points you have in your wallet!
Use '**{0}wallets**' to show how many points everyone has!
Use '**{0}resets**' to show how many resets everyone has.

Points earned are multiplied if you guess within 4 guesses!
When anyone reaches {4} points, a disaster will occur for a random player. Their wallet will be reset to {5} points if they are not insured.
All wallets are reset to {5} points once someone reaches {6} points.", await GetServerPrefix(serverId), levelTwoReward, stealAmount, insuranceCost, disasterMark, resetBalance, resetMark)));

						// update storm to level 2
						OngoingStormsLevel[channelId] = 2;
					}
					else if (actualLevel == 2)
					{
						// if user has guessed, get count; otherwise, set count to 0
						int guessCount;
						if (!OngoingStormsUserGuessCount[channelId].TryGetValue(discordId, out guessCount))
							 guessCount = 0;

						// store user's guess count as 1 if it's their first guess
						if (guessCount == 0)
						{
							guessCount = 1;
							OngoingStormsUserGuessCount[channelId].Add(discordId, guessCount);
						}
						// if it's not the users first guess, increment the count
						else
						{
							OngoingStormsUserGuessCount[channelId][discordId] += 1;
							guessCount = OngoingStormsUserGuessCount[channelId][discordId];
						}

						double multiplier = 1;
						if (guessCount == 1)
							multiplier = 10;
						else if (guessCount == 2)
							multiplier = 5;
						else if (guessCount == 3)
							multiplier = 2.5;
						else if (guessCount == 4)
							multiplier = 1.25;

						if (guess == OngoingStormsWinningNumber[channelId])
						{
							// give user points for level 2 (default levelTwoReward, or points bet)
							double reward = levelTwoReward;
							if (bet != null && bet.Value > levelTwoReward)
								reward = bet.Value;

							playerData.Wallet += reward * multiplier;
							await _db.SaveChangesAsync();

							string multiplierStr = "";
							if (multiplier > 1)
								multiplierStr = $" ( **{guessCount} GUESSES:** {reward} points x{multiplier} multiplier )";

							purgeCollection.Add(await((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync($"<@!{discordId}>, you guessed correctly and earned {reward * multiplier} points!" + multiplierStr));

							// end storm at level 3
							OngoingStormsLevel[channelId] = 3;
							EndStorm(channelId);
						}
						else
						{
							string message = $"<@!{discordId}>, you guessed incorrectly";

							if (bet != null)
							{
								message += $" and lost {bet.Value} points.\n";

								// take points from user if they bet
								double newWallet = playerData.Wallet - bet.Value;
								if (newWallet < 0)
									playerData.Wallet = 0;
								else
									playerData.Wallet = newWallet;

								await _db.SaveChangesAsync();
							}
							else
							{
								message += ".\n";
							}

							message += "The winning number is ";

							if (guess < OngoingStormsWinningNumber[channelId])
							{
								message += "greater than ";
							}
							else
							{
								message += "less than ";
							}

							message += guess + ".";

							purgeCollection.Add(await((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync(message));
						}
					}

					await CheckForReset(guild, serverId, discordId, channelId);
					await CheckForDisaster(serverId, discordId, channelId, hadDisasterMark);
				}
			}
		}

		public async Task TryToSteal(SocketGuild guild, ulong serverId, ulong discordId, ulong channelId)
		{
			int actualLevel;

			// make sure that there is an ongoing storm on level two
			if (OngoingStormsLevel.TryGetValue(channelId, out actualLevel) && actualLevel == 2)
			{
				// make sure user has not stolen in the last ten seconds
				if (!OngoingStormsUsersWaitingForStealTimeLimit[channelId].Contains(discordId))
				{
					// add user to list of users waiting and trigger removal after set time in seconds
					OngoingStormsUsersWaitingForStealTimeLimit[channelId].Add(discordId);
					StartUsersStealTimeLimitCountdown(channelId, discordId);

					StormPlayerDataEntity playerData = await AddPlayerToDbTableIfNotExist(serverId, discordId);
					bool hadDisasterMark = playerData.Wallet >= disasterMark;

					List<StormPlayerDataEntity> allPlayerData = await GetAllStormPlayerDataEntities(serverId);
					StormPlayerDataEntity topPlayer = allPlayerData.OrderByDescending(player => player.Wallet).First();

					// do not let users steal from themselves
					if (topPlayer.DiscordID != discordId)
					{
						// set top player's wallet and criminal's wallet
						double oldWallet = topPlayer.Wallet;
						double newWallet = oldWallet - stealAmount;
						double diff;
						if (newWallet < 0)
						{
							topPlayer.Wallet = 0;
							diff = oldWallet;
						}
						else
						{
							topPlayer.Wallet = newWallet;
							diff = stealAmount;
						}

						playerData.Wallet += diff;

						await _db.SaveChangesAsync();

						purgeCollection.Add(await ((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync($"<@!{discordId}>, you stole {diff} points from <@!{topPlayer.DiscordID}>!"));

						await CheckForReset(guild, serverId, discordId, channelId);
						await CheckForDisaster(serverId, discordId, channelId, hadDisasterMark);
					}
				}
				else
					purgeCollection.Add(await ((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync($"<@!{discordId}>, please wait {stealTimeLimitInSeconds} seconds before stealing again."));
			}
		}

		private async Task CheckForReset(SocketGuild guild, ulong serverId, ulong discordId, ulong channelId)
		{
			StormPlayerDataEntity playerData = await GetStormPlayerDataEntity(serverId, discordId);

			// increment players reset count, set everyones wallets back to base amount, and give appropriate roles
			if (playerData.Wallet >= resetMark)
			{
				// end the ongoing storm if there is a reset
				EndStorm(channelId);

				playerData.ResetCount++;

				// give everyone the base wallet amount and no insurance
				List<StormPlayerDataEntity> allPlayerData = await GetAllStormPlayerDataEntities(serverId);
				foreach (StormPlayerDataEntity player in allPlayerData)
				{
					player.Wallet = resetBalance;
					player.HasInsurance = false;
				}

				await _db.SaveChangesAsync();

				ulong mostRecentRoleID = await GetStormsMostRecentResetRoleID(serverId);
				ulong mostResetsRoleID = await GetStormsMostResetsRoleID(serverId);

				// unassign both roles from everyone
				await UnassignRoleFromAllMembers(mostResetsRoleID, guild);
				await UnassignRoleFromAllMembers(mostRecentRoleID, guild);

				// assign most recent reset role to the resetting player
				List<ulong> resettingPlayer = new List<ulong>();
				resettingPlayer.Add(playerData.DiscordID);
				await GiveUsersRole(mostRecentRoleID, resettingPlayer, guild);

				// assign the most resets role to the player(s) with the most resets
				int topScore = allPlayerData.OrderByDescending(player => player.ResetCount).First().ResetCount;
				List<ulong> topPlayersDiscordIDs = allPlayerData.Where(player => player.ResetCount == topScore).Select(player => player.DiscordID).ToList();
				await GiveUsersRole(mostResetsRoleID, topPlayersDiscordIDs, guild);

				string topPlayersStr = "";
				foreach (ulong DiscordID in topPlayersDiscordIDs)
				{
					topPlayersStr += string.Format(@"<@!{0}>, ", DiscordID);
				}

				// display reset message and post role announcements; this message is rare and therefore is not purged with other messages
				await ((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync(rotating_light.ToString() + rotating_light.ToString() + rotating_light.ToString() + " __**RESET TRIGGERED**__ " + rotating_light.ToString() + rotating_light.ToString() + rotating_light.ToString() + string.Format(@"

Congratulations <@!{0}>, you passed {1} points and triggered a reset! You have been given the <@&{2}> role. Everyone now has {3} points in their wallet and no insurance.

{4}you currently have the <@&{5}> role.", playerData.DiscordID, resetMark, mostRecentRoleID, resetBalance, topPlayersStr, mostResetsRoleID));
			}
		}

		private async Task CheckForDisaster(ulong serverId, ulong discordId, ulong channelId, bool hadDisasterMark)
		{
			StormPlayerDataEntity playerData = await GetStormPlayerDataEntity(serverId, discordId);

			if (playerData.Wallet >= disasterMark && !hadDisasterMark)
			{
				// reset random player's wallet if they are uninsured
				List<StormPlayerDataEntity> allPlayerData = await GetAllStormPlayerDataEntities(serverId);
				Random random = new Random();
				int randomIndex = random.Next(0, allPlayerData.Count);

				string theyYouStr = "";
				string theirYour = "";
				string onPersonAffected = "";
				if (allPlayerData[randomIndex].DiscordID == discordId)
				{
					theyYouStr = " You";
					theirYour = " your";
					onPersonAffected = " on yourself";
				}
				else
				{
					theyYouStr = " They";
					theirYour = " their";
					onPersonAffected = $" for <@!{allPlayerData[randomIndex].DiscordID}>";
				}

				string insuredOrNotStr = "";
				if (!allPlayerData[randomIndex].HasInsurance)
				{
					insuredOrNotStr = theyYouStr + " were not insured and" + theirYour + " wallet has been reset!";
					allPlayerData[randomIndex].Wallet = resetBalance;
				}
				else
				{
					insuredOrNotStr = " However," + theyYouStr + " were insured and not affected.";
				}

				await _db.SaveChangesAsync();

				purgeCollection.Add(await ((IMessageChannel)_client.GetChannel(channelId)).SendMessageAsync($"<@!{discordId}>, you caused a disaster" + onPersonAffected + $" since you passed {disasterMark} points!" + insuredOrNotStr));
			}
		}

		#region QUERIES
		public async Task<StormPlayerDataEntity> AddPlayerToDbTableIfNotExist(ulong serverID, ulong discordID)
		{
			StormPlayerDataEntity playerData = await GetStormPlayerDataEntity(serverID, discordID);

			if (playerData == null)
			{
				StormPlayerDataEntity newPlayerData = new StormPlayerDataEntity()
				{
					ServerID = serverID,
					DiscordID = discordID,
					Wallet = 0,
					ResetCount = 0,
					HasInsurance = false
				};

				_db.StormPlayerData.Add(newPlayerData);
				await _db.SaveChangesAsync();

				return newPlayerData;
			}

			return playerData;
		}

		public async Task<StormPlayerDataEntity> GetStormPlayerDataEntity(ulong serverID, ulong discordID)
		{
			return await _db.StormPlayerData
				.AsQueryable()
				.Where(player => player.ServerID == serverID && player.DiscordID == discordID)
				.SingleOrDefaultAsync();
		}

		public async Task<List<StormPlayerDataEntity>> GetAllStormPlayerDataEntities(ulong serverID)
		{
			return await _db.StormPlayerData
				.AsQueryable()
				.Where(player => player.ServerID == serverID)
				.AsAsyncEnumerable()
				.ToListAsync();
		}

		public async Task<bool> GetServerToggleStorms(SocketCommandContext context)
		{
			if (!context.IsPrivate)
			{
				bool flag = await _db.Servers
				.AsQueryable()
				.Where(s => s.ServerID == context.Guild.Id)
				.Select(s => s.ToggleStorms)
				.SingleAsync();

				if (!flag)
					Console.WriteLine($"Command will be ignored: Admin toggled off. Server: {context.Guild.Name} ({context.Guild.Id})");

				return flag;
			}
			else
				return true;
		}

		public async Task<bool> GetServerAllowServerPermissionStorms(SocketCommandContext context)
		{
			if (!context.IsPrivate)
			{
				bool flag = await _db.Servers
				.AsQueryable()
				.Where(s => s.ServerID == context.Guild.Id)
				.Select(s => s.AllowServerPermissionStorms)
				.SingleAsync();

				if (!flag)
					Console.WriteLine($"Command will be ignored: Bot ignoring server. Server: {context.Guild.Name} ({context.Guild.Id})");

				return flag;
			}
			else
				return true;
		}

		public async Task<ulong> GetStormsMostResetsRoleID(ulong serverId)
		{
			return await _db.Servers
				.AsQueryable()
				.Where(s => s.ServerID == serverId)
				.Select(s => s.StormsMostResetsRoleID)
				.SingleOrDefaultAsync();
		}

		public async Task<ulong> GetStormsMostRecentResetRoleID(ulong serverId)
		{
			return await _db.Servers
				.AsQueryable()
				.Where(s => s.ServerID == serverId)
				.Select(s => s.StormsMostRecentResetRoleID)
				.SingleOrDefaultAsync();
		}
		#endregion
	}
}
