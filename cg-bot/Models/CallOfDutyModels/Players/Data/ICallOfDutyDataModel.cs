﻿
namespace cg_bot.Models.CallOfDutyModels.Players.Data
{
	public interface ICallOfDutyDataModel
	{
		object ParticipatingAccountsFileLock { get; set; }

		object SavedPlayerDataFileLock { get; set; }

		string GameName { get; }

		string GameAbbrevAPI { get; }

		string ModeAbbrevAPI { get; }

	}
}