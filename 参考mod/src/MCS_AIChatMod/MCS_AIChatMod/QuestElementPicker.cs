using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using JSONClass;
using KBEngine;
using KillSystem;

namespace MCS_AIChatMod;

public static class QuestElementPicker
{
	public class PickedElements
	{
		public List<PickedItem> Items = new List<PickedItem>();

		public List<PickedLocation> Locations = new List<PickedLocation>();

		public List<PickedNpc> Npcs = new List<PickedNpc>();

		public NarrativeMaterialContext NarrativeContext = new NarrativeMaterialContext();
	}

	public class PickedItem
	{
		public int Id;

		public string Name;

		public string Category;

		public int Quality;

		public string Desc;

		public string Effect;
	}

	public class PickedLocation
	{
		public string Id;

		public string Name;

		public string RegionName;
	}

	public class PickedNpc
	{
		public string Id;

		public string Name;

		public string Relation;

		public string Faction;

		public string XiuWei;

		public string Gender;

		public string SourceBucket;
	}

	public class FunctionalItemCandidate
	{
		public string FunctionTag;

		public string Scarcity;

		public string RealmBand;

		public List<int> CandidateItemIds = new List<int>();
	}

	public class TimeCandidate
	{
		public string Label;

		public string Summary;

		public int MinMonths;

		public int MaxMonths;

		public bool RequiresBeforeBreakthrough;
	}

	public class NarrativeMaterialContext
	{
		public List<PickedNpc> CandidateNpcs = new List<PickedNpc>();

		public List<PickedLocation> CandidateLocations = new List<PickedLocation>();

		public List<NarrativeConcreteItemCandidate> ConcreteStoryItems = new List<NarrativeConcreteItemCandidate>();

		public List<TimeCandidate> TimeCandidates = new List<TimeCandidate>();
	}

	public class NarrativeConcreteItemCandidate
	{
		public int ItemId;

		public string Name;

		public int Quality;

		public string OfficialDescription;

		public string Effect;

		public string Scarcity;
	}

	public class ConcreteItemOption
	{
		public string FunctionTag;

		public int ItemId;

		public string Name;

		public int Quality;

		public string Effect;
	}

	public class BeatExecutionContext
	{
		public List<PickedNpc> AllowedNpcs = new List<PickedNpc>();

		public List<PickedLocation> AllowedLocations = new List<PickedLocation>();

		public List<ConcreteItemOption> ConcreteItems = new List<ConcreteItemOption>();

		public List<TimeCandidate> TimeCandidates = new List<TimeCandidate>();
	}

	private static readonly Random _rng = new Random();

	private static readonly object _officialLocationCacheLock = new object();

	private static Dictionary<string, PickedLocation> _officialLocationPoolCache;

	private static Dictionary<string, List<string>> _officialLocationNameLookupCache;

	private static int _officialLocationPoolBuildCountForTest;

	private static void LogQuestToolWarning(string context, Exception ex)
	{
		ManualLogSource logger = AIChatManager.logger;
		if (logger != null)
		{
			logger.LogWarning((object)$"[QuestElementPicker] {context}: {ex}");
		}
	}

	public static PickedElements Pick(NPCInfo npcInfo, QuestProgramState programState)
	{
		return PickInternal(npcInfo, GetRecentItemIds(programState), GetRecentSceneIds(programState));
	}

	private static PickedElements PickInternal(NPCInfo npcInfo, HashSet<int> recentItemIds, HashSet<string> recentSceneIds)
	{
		PickedElements result = new PickedElements();
		int npcItemLevel = Math.Max(1, (npcInfo.Level - 1) / 3 + 1);
		result.Items = PickItems(npcInfo.NpcID, npcItemLevel, recentItemIds);
		if (result.Items.Count == 0)
		{
			result.Items = PickItems(npcInfo.NpcID, 0, null);
		}
		result.Locations = PickLocations(npcInfo, recentSceneIds);
		if (result.Locations.Count == 0)
		{
			result.Locations = PickLocationsAny();
		}
		result.Npcs = PickNpcs(npcInfo.NpcID);
		result.NarrativeContext = BuildNarrativeMaterialContext(result, npcInfo);
		AIChatManager.logger.LogInfo((object)$"[QuestElementPicker] 盲盒抽取: {result.Items.Count}物品, {result.Locations.Count}场景, {result.Npcs.Count}NPC");
		return result;
	}

	public static string FormatForPrompt(PickedElements elements)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("=== \ud83c\udfb2 本次可用素材（物品ID和场景ID必须从此处选择，禁止编造） ===");
		foreach (PickedItem item in elements.Items)
		{
			string desc = (string.IsNullOrEmpty(item.Desc) ? "" : (" - " + item.Desc));
			string effect = (string.IsNullOrEmpty(item.Effect) ? "" : (" 效果:" + item.Effect));
			sb.AppendLine($"\ud83d\udce6 物品ID:{item.Id} {item.Name}({item.Category},品阶{item.Quality}){desc}{effect}");
		}
		foreach (PickedLocation loc in elements.Locations)
		{
			sb.AppendLine("\ud83d\uddfa\ufe0f 场景ID:" + loc.Id + " " + loc.Name);
		}
		foreach (PickedNpc npc in elements.Npcs)
		{
			string faction = (string.IsNullOrEmpty(npc.Faction) ? "" : (" " + npc.Faction));
			string xiuwei = (string.IsNullOrEmpty(npc.XiuWei) ? "" : (" " + npc.XiuWei));
			string gender = (string.IsNullOrEmpty(npc.Gender) ? "" : (" " + npc.Gender));
			string relation = (string.IsNullOrEmpty(npc.Relation) ? "" : ("（" + npc.Relation + "）"));
			sb.AppendLine("\ud83d\udc64 NPC_ID:" + npc.Id + " " + npc.Name + relation + faction + xiuwei + gender);
		}
		sb.AppendLine();
		sb.AppendLine("不需要用完所有素材，选最适合故事的即可。奖励物品也从此处选择。");
		return sb.ToString();
	}

	public static string FormatNarrativeMaterialsForPrompt(PickedElements elements)
	{
		NarrativeMaterialContext context = elements?.NarrativeContext ?? new NarrativeMaterialContext();
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("=== 主线剧情编排可用素材 ===");
		if (context.CandidateNpcs.Count > 0)
		{
			sb.AppendLine("【NPC 候选池】");
			foreach (PickedNpc npc in context.CandidateNpcs)
			{
				string relation = (string.IsNullOrWhiteSpace(npc.Relation) ? string.Empty : ("（" + npc.Relation + "）"));
				string faction = (string.IsNullOrWhiteSpace(npc.Faction) ? string.Empty : (" " + npc.Faction));
				string cultivation = (string.IsNullOrWhiteSpace(npc.XiuWei) ? string.Empty : (" " + npc.XiuWei));
				sb.AppendLine("- NPC_ID:" + npc.Id + " " + npc.Name + relation + faction + cultivation);
			}
		}
		if (context.CandidateLocations.Count > 0)
		{
			sb.AppendLine("【场景候选池】");
			foreach (PickedLocation location in context.CandidateLocations)
			{
				sb.AppendLine("- 场景ID:" + location.Id + " " + location.Name);
			}
		}
		if (context.ConcreteStoryItems.Count > 0)
		{
			sb.AppendLine("【具体物品候选池】");
			foreach (NarrativeConcreteItemCandidate item in context.ConcreteStoryItems)
			{
				sb.AppendLine($"- {item.Name} | 品阶:{item.Quality} | 稀缺度:{item.Scarcity} | 用途:{item.Effect}");
			}
		}
		if (context.TimeCandidates.Count > 0)
		{
			sb.AppendLine("【时间候选池】");
			foreach (TimeCandidate time in context.TimeCandidates)
			{
				sb.AppendLine($"- {time.Label} | {time.Summary} | 月份范围:{time.MinMonths}-{time.MaxMonths}");
			}
		}
		sb.AppendLine();
		sb.AppendLine("第一阶段只能围绕这些 NPC、场景、具体物品与时间意图进行章节编排；具体物品优先参考候选池里的官方介绍；即使你知道游戏里还存在别的官方场景或官方物品，只要这次素材库没给出，也不要写。");
		return sb.ToString();
	}

	public static BeatExecutionContext BuildBeatExecutionContext(PickedElements elements, MainlineBeat beat)
	{
		BeatExecutionContext context = new BeatExecutionContext();
		if (elements == null || beat == null)
		{
			return context;
		}
		HashSet<int> allowedNpcIds = new HashSet<int>(beat.WorldContract?.AllowedNpcIds ?? new List<int>());
		if (allowedNpcIds.Count == 0 && beat.KeyNpcIds != null)
		{
			allowedNpcIds = new HashSet<int>(beat.KeyNpcIds);
		}
		HashSet<string> allowedSceneIds = new HashSet<string>(beat.WorldContract?.AllowedSceneIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
		if (allowedSceneIds.Count == 0 && beat.KeyLocationIds != null)
		{
			allowedSceneIds = new HashSet<string>(beat.KeyLocationIds.Where((string id) => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
		}
		context.AllowedNpcs = elements.Npcs.Where((PickedNpc npc) => npc != null && int.TryParse(npc.Id, out var result) && allowedNpcIds.Contains(result)).ToList();
		context.AllowedLocations = elements.Locations.Where((PickedLocation location) => location != null && allowedSceneIds.Contains(location.Id ?? string.Empty)).ToList();
		context.ConcreteItems = (elements.NarrativeContext?.ConcreteStoryItems ?? new List<NarrativeConcreteItemCandidate>()).Select((NarrativeConcreteItemCandidate item) => new ConcreteItemOption
		{
			FunctionTag = string.Empty,
			ItemId = item.ItemId,
			Name = item.Name,
			Quality = item.Quality,
			Effect = item.Effect
		}).Take(12).ToList();
		string targetTimeBand = beat.TimeIntent?.TimeBand;
		IEnumerable<TimeCandidate> times = elements.NarrativeContext?.TimeCandidates ?? new List<TimeCandidate>();
		if (!string.IsNullOrWhiteSpace(targetTimeBand))
		{
			times = times.Where((TimeCandidate candidate) => string.Equals(candidate.Label, targetTimeBand, StringComparison.OrdinalIgnoreCase));
		}
		context.TimeCandidates = times.ToList();
		return context;
	}

	public static string FormatBeatExecutionContextForPrompt(BeatExecutionContext context)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("=== 当前 beat 的游戏落地上下文 ===");
		if (context?.AllowedNpcs != null && context.AllowedNpcs.Count > 0)
		{
			sb.AppendLine("【允许使用的 NPC】");
			foreach (PickedNpc npc in context.AllowedNpcs)
			{
				sb.AppendLine("- NPC_ID:" + npc.Id + " " + npc.Name);
			}
		}
		if (context?.AllowedLocations != null && context.AllowedLocations.Count > 0)
		{
			sb.AppendLine("【允许使用的场景】");
			foreach (PickedLocation location in context.AllowedLocations)
			{
				sb.AppendLine("- 场景ID:" + location.Id + " " + location.Name);
			}
		}
		if (context?.ConcreteItems != null && context.ConcreteItems.Count > 0)
		{
			sb.AppendLine("【资源功能映射到的具体物品】");
			foreach (ConcreteItemOption item in context.ConcreteItems)
			{
				sb.AppendLine($"- 物品ID:{item.ItemId} {item.Name}（品阶{item.Quality}，效果：{item.Effect}）");
			}
		}
		if (context?.TimeCandidates != null && context.TimeCandidates.Count > 0)
		{
			sb.AppendLine("【当前 beat 可落地的时间解释】");
			foreach (TimeCandidate time in context.TimeCandidates)
			{
				sb.AppendLine($"- {time.Label} | {time.Summary} | 月份范围:{time.MinMonths}-{time.MaxMonths}");
			}
		}
		return sb.ToString();
	}

	private static NarrativeMaterialContext BuildNarrativeMaterialContext(PickedElements elements, NPCInfo npcInfo)
	{
		return new NarrativeMaterialContext
		{
			CandidateNpcs = (elements?.Npcs?.Take(6).ToList() ?? new List<PickedNpc>()),
			CandidateLocations = (elements?.Locations?.Take(6).ToList() ?? new List<PickedLocation>()),
			ConcreteStoryItems = BuildNarrativeConcreteItemPool(elements?.Items, npcInfo),
			TimeCandidates = BuildTimeCandidatePool(npcInfo)
		};
	}

	private static List<NarrativeConcreteItemCandidate> BuildNarrativeConcreteItemPool(IEnumerable<PickedItem> items, NPCInfo npcInfo)
	{
		List<PickedItem> itemList = (items ?? Enumerable.Empty<PickedItem>()).Where((PickedItem pickedItem) => pickedItem != null).ToList();
		List<FunctionalItemCandidate> grouped = BuildFunctionalItemPool(itemList, npcInfo);
		Dictionary<int, PickedItem> itemById = (from pickedItem in itemList
			where pickedItem.Id > 0
			group pickedItem by pickedItem.Id).ToDictionary((IGrouping<int, PickedItem> group) => group.Key, (IGrouping<int, PickedItem> group) => group.First());
		List<NarrativeConcreteItemCandidate> selected = new List<NarrativeConcreteItemCandidate>();
		HashSet<int> seenItemIds = new HashSet<int>();
		foreach (FunctionalItemCandidate functionGroup in grouped)
		{
			foreach (int itemId in functionGroup.CandidateItemIds)
			{
				if (itemById.TryGetValue(itemId, out var item) && seenItemIds.Add(itemId))
				{
					selected.Add(new NarrativeConcreteItemCandidate
					{
						ItemId = item.Id,
						Name = item.Name,
						Quality = item.Quality,
						OfficialDescription = (item.Desc ?? string.Empty),
						Effect = (string.IsNullOrWhiteSpace(item.Effect) ? (item.Desc ?? string.Empty) : item.Effect),
						Scarcity = ResolveItemScarcity(item.Quality)
					});
				}
			}
		}
		return selected.OrderBy((NarrativeConcreteItemCandidate narrativeConcreteItemCandidate) => narrativeConcreteItemCandidate.Name, StringComparer.OrdinalIgnoreCase).Take(6).ToList();
	}

	private static List<FunctionalItemCandidate> BuildFunctionalItemPool(IEnumerable<PickedItem> items, NPCInfo npcInfo)
	{
		Dictionary<string, FunctionalItemCandidate> grouped = new Dictionary<string, FunctionalItemCandidate>(StringComparer.OrdinalIgnoreCase);
		foreach (PickedItem item in items ?? Enumerable.Empty<PickedItem>())
		{
			if (item != null)
			{
				string functionTag = MapItemToFunctionTag(item);
				if (!grouped.TryGetValue(functionTag, out var candidate))
				{
					candidate = (grouped[functionTag] = new FunctionalItemCandidate
					{
						FunctionTag = functionTag,
						Scarcity = ResolveItemScarcity(item.Quality),
						RealmBand = ResolveRealmBand(npcInfo?.Level ?? 1)
					});
				}
				if (!candidate.CandidateItemIds.Contains(item.Id))
				{
					candidate.CandidateItemIds.Add(item.Id);
				}
			}
		}
		return grouped.Values.OrderBy((FunctionalItemCandidate functionalItemCandidate2) => functionalItemCandidate2.FunctionTag, StringComparer.OrdinalIgnoreCase).ToList();
	}

	private static List<TimeCandidate> BuildTimeCandidatePool(NPCInfo npcInfo)
	{
		int remainingLifeMonths = ResolveRemainingLifeMonths(npcInfo);
		int shortMaxMonths = Math.Max(1, (int)Math.Floor((double)remainingLifeMonths * 0.1));
		shortMaxMonths = Math.Min(shortMaxMonths, remainingLifeMonths);
		int mediumMinMonths = Math.Min(remainingLifeMonths, shortMaxMonths + 1);
		int mediumMaxMonths = Math.Max(mediumMinMonths, (int)Math.Floor((double)remainingLifeMonths * 0.35));
		mediumMaxMonths = Math.Min(mediumMaxMonths, remainingLifeMonths);
		int longMinMonths = Math.Min(remainingLifeMonths, mediumMaxMonths + 1);
		return new List<TimeCandidate>
		{
			new TimeCandidate
			{
				Label = "短期",
				Summary = "适合在相对靠近的时间内推进一次阶段性事件，但不必压缩成几个月内的插曲",
				MinMonths = 1,
				MaxMonths = shortMaxMonths
			},
			new TimeCandidate
			{
				Label = "中期",
				Summary = "适合跨多年铺垫、调查、往返与关系演变，但不能超过当前NPC的剩余寿命",
				MinMonths = mediumMinMonths,
				MaxMonths = mediumMaxMonths
			},
			new TimeCandidate
			{
				Label = "长期",
				Summary = "适合贯穿更长修仙岁月的道途推进、来往、闭关与关系变化，只要不超过当前NPC的剩余寿命",
				MinMonths = longMinMonths,
				MaxMonths = remainingLifeMonths
			}
		};
	}

	internal static int ResolveTimeBandMaxMonthsForNpc(NPCInfo npcInfo, string timeBand)
	{
		List<TimeCandidate> candidates = BuildTimeCandidatePool(npcInfo);
		if (string.IsNullOrWhiteSpace(timeBand))
		{
			return (candidates.Count != 0) ? candidates[candidates.Count - 1].MaxMonths : 0;
		}
		return candidates.FirstOrDefault((TimeCandidate candidate) => candidate != null && string.Equals(candidate.Label, timeBand.Trim(), StringComparison.OrdinalIgnoreCase))?.MaxMonths ?? ((candidates.Count != 0) ? candidates[candidates.Count - 1].MaxMonths : 0);
	}

	private static string MapItemToFunctionTag(PickedItem item)
	{
		string text = (item?.Name + " " + item?.Category + " " + item?.Desc + " " + item?.Effect).ToLowerInvariant();
		if (text.Contains("疗伤") || text.Contains("回血") || text.Contains("回复") || text.Contains("治疗"))
		{
			return "疗伤资源";
		}
		if (text.Contains("战斗") || text.Contains("攻击") || text.Contains("伤害") || text.Contains("斗法") || text.Contains("护盾"))
		{
			return "战斗强化资源";
		}
		if (text.Contains("突破") || text.Contains("结丹") || text.Contains("筑基") || text.Contains("凝婴"))
		{
			return "突破辅助资源";
		}
		if (text.Contains("清心") || text.Contains("宁神") || text.Contains("明心") || text.Contains("心境"))
		{
			return "心境稳定资源";
		}
		if ((text.Contains("丹") || text.Contains("灵草") || text.Contains("药") || text.Contains("矿")) && item != null && item.Quality >= 4)
		{
			return "稀有炼丹材料";
		}
		if (text.Contains("修为") || text.Contains("灵气") || text.Contains("修炼"))
		{
			return "修为增益资源";
		}
		return "常规宗门供给";
	}

	private static string ResolveItemScarcity(int quality)
	{
		if (quality >= 5)
		{
			return "极稀有";
		}
		if (quality >= 4)
		{
			return "稀有";
		}
		if (quality >= 3)
		{
			return "较少";
		}
		return "常见";
	}

	private static string ResolveRealmBand(int level)
	{
		if (level >= 13)
		{
			return "元婴-化神";
		}
		if (level >= 10)
		{
			return "金丹-元婴";
		}
		if (level >= 7)
		{
			return "筑基-金丹";
		}
		return "炼气-筑基";
	}

	private static int ResolveRemainingLifeMonths(NPCInfo npcInfo)
	{
		int remainingLifeYears = 0;
		if (npcInfo != null && npcInfo.NpcID > 0 && NpcJieSuanManager.inst != null)
		{
			remainingLifeYears = NpcJieSuanManager.inst.GetNpcShengYuTime(npcInfo.NpcID);
		}
		if (remainingLifeYears <= 0 && npcInfo != null)
		{
			remainingLifeYears = npcInfo.ShouYuan - npcInfo.Age / 12;
		}
		return Math.Max(1, remainingLifeYears * 12);
	}

	private static List<PickedItem> PickItems(int npcId, int playerLevel, HashSet<int> recentItemIds)
	{
		List<PickedItem> result = new List<PickedItem>();
		recentItemIds = recentItemIds ?? new HashSet<int>();
		int minQ = Math.Max(1, playerLevel - 1);
		int maxQ = Math.Min(6, playerLevel + 1);
		List<KeyValuePair<int, _ItemJsonData>> interestedCandidates = new List<KeyValuePair<int, _ItemJsonData>>();
		List<KeyValuePair<int, _ItemJsonData>> normalCandidates = new List<KeyValuePair<int, _ItemJsonData>>();
		foreach (KeyValuePair<int, _ItemJsonData> kvp in _ItemJsonData.DataDict)
		{
			_ItemJsonData item = kvp.Value;
			if (item.vagueType > 0 && (playerLevel == 0 || (item.quality >= minQ && item.quality <= maxQ)) && !recentItemIds.Contains(kvp.Key) && !string.IsNullOrEmpty(item.name) && item.name.Length > 1)
			{
				int interest = 0;
				try
				{
					interest = jsonData.instance.GetMonstarInterestingItem(npcId, kvp.Key);
				}
				catch (Exception ex)
				{
					LogQuestToolWarning($"PickItems GetMonstarInterestingItem npcId={npcId}, itemId={kvp.Key}", ex);
				}
				if (interest > 0)
				{
					interestedCandidates.Add(kvp);
				}
				else
				{
					normalCandidates.Add(kvp);
				}
			}
		}
		AIChatManager.logger.LogInfo((object)$"[QuestElementPicker] NPC感兴趣物品候选: {interestedCandidates.Count}个, 普通物品候选: {normalCandidates.Count}个");
		List<KeyValuePair<int, _ItemJsonData>> candidates = new List<KeyValuePair<int, _ItemJsonData>>();
		Shuffle(interestedCandidates);
		int interestTakeCount = Math.Max(2, interestedCandidates.Count / 2);
		candidates.AddRange(interestedCandidates.Take(interestTakeCount));
		Shuffle(normalCandidates);
		candidates.AddRange(normalCandidates.Take(10));
		if (candidates.Count == 0 && normalCandidates.Count > 0)
		{
			candidates.AddRange(normalCandidates);
		}
		int count = _rng.Next(3, 5);
		Shuffle(candidates);
		HashSet<int> usedTypes = new HashSet<int>();
		HashSet<string> usedItemKeys = new HashSet<string>();
		foreach (KeyValuePair<int, _ItemJsonData> kvp2 in candidates)
		{
			if (result.Count >= count)
			{
				break;
			}
			string itemKey = $"{kvp2.Value.name}#{kvp2.Value.quality}";
			if (!usedItemKeys.Contains(itemKey) && (!usedTypes.Contains(kvp2.Value.type) || result.Count <= 0 || candidates.Count <= count))
			{
				usedTypes.Add(kvp2.Value.type);
				usedItemKeys.Add(itemKey);
				result.Add(new PickedItem
				{
					Id = kvp2.Key,
					Name = kvp2.Value.name,
					Category = GetItemCategoryName(kvp2.Value.type),
					Quality = kvp2.Value.quality,
					Desc = TruncateString(kvp2.Value.desc, 50),
					Effect = TruncateString(kvp2.Value.desc2, 60)
				});
			}
		}
		if (result.Count == 0 && candidates.Count > 0)
		{
			KeyValuePair<int, _ItemJsonData> kvp3 = candidates.FirstOrDefault((KeyValuePair<int, _ItemJsonData> pair) => !string.IsNullOrEmpty(pair.Value.name));
			if (string.IsNullOrEmpty(kvp3.Value.name))
			{
				kvp3 = candidates[0];
			}
			result.Add(new PickedItem
			{
				Id = kvp3.Key,
				Name = kvp3.Value.name,
				Category = GetItemCategoryName(kvp3.Value.type),
				Quality = kvp3.Value.quality,
				Desc = TruncateString(kvp3.Value.desc, 50),
				Effect = TruncateString(kvp3.Value.desc2, 60)
			});
		}
		return result;
	}

	private static List<PickedLocation> PickLocations(NPCInfo npc, HashSet<string> recentLocationIds)
	{
		List<PickedLocation> result = new List<PickedLocation>();
		if (npc == null)
		{
			return result;
		}
		recentLocationIds = recentLocationIds ?? new HashSet<string>();
		HashSet<string> pickedIds = new HashSet<string>(StringComparer.Ordinal);
		Dictionary<string, PickedLocation> officialPool = BuildOfficialLocationPool();
		TryAddPickedLocation(result, pickedIds, recentLocationIds, officialPool, GetNpcCurrentScene(npc.NpcID));
		List<string> preferredLocationIds = GetNpcPreferredLocationIds(npc.NpcID);
		Shuffle(preferredLocationIds);
		foreach (string locationId in preferredLocationIds)
		{
			if (result.Count >= 2)
			{
				break;
			}
			TryAddPickedLocation(result, pickedIds, recentLocationIds, officialPool, locationId);
		}
		List<PickedLocation> fallbackLocations = officialPool.Values.Where((PickedLocation loc) => !pickedIds.Contains(loc.Id) && !recentLocationIds.Contains(loc.Id)).ToList();
		Shuffle(fallbackLocations);
		int targetCount = Math.Min(_rng.Next(3, 5), Math.Max(1, officialPool.Count));
		foreach (PickedLocation location in fallbackLocations)
		{
			if (result.Count >= targetCount)
			{
				break;
			}
			result.Add(location);
			pickedIds.Add(location.Id);
		}
		if (result.Count < Math.Min(3, officialPool.Count))
		{
			foreach (PickedLocation location2 in officialPool.Values.OrderBy((PickedLocation _) => _rng.Next()))
			{
				if (!pickedIds.Contains(location2.Id))
				{
					result.Add(location2);
					pickedIds.Add(location2.Id);
					if (result.Count >= Math.Min(3, officialPool.Count))
					{
						break;
					}
				}
			}
		}
		return result;
	}

	private static List<PickedLocation> PickLocationsAny()
	{
		List<PickedLocation> officialPool = BuildOfficialLocationPool().Values.ToList();
		Shuffle(officialPool);
		int count = Math.Min(_rng.Next(3, 5), officialPool.Count);
		return officialPool.Take(count).ToList();
	}

	private static List<PickedNpc> PickNpcs(int questNpcId)
	{
		List<PickedNpc> result = new List<PickedNpc>();
		List<PickedNpc> candidates = new List<PickedNpc>();
		HashSet<string> seenIds = new HashSet<string>();
		Dictionary<string, int> rejectedReasons = new Dictionary<string, int>(StringComparer.Ordinal);
		int consideredCount = 0;
		try
		{
			JSONObject npcData = NpcJieSuanManager.inst?.GetNpcData(questNpcId);
			if (npcData != null)
			{
				AddNpcCandidate(questNpcId, (!npcData.HasField("ShiFu")) ? "" : npcData["ShiFu"]?.Str, "师傅", "Relation", seenIds, candidates, rejectedReasons, ref consideredCount);
				AddNpcCandidate(questNpcId, (!npcData.HasField("DaoLvID")) ? "" : npcData["DaoLvID"]?.Str, "道侣", "Relation", seenIds, candidates, rejectedReasons, ref consideredCount);
				if (npcData.HasField("TuDi") && npcData["TuDi"] != null && npcData["TuDi"].IsArray)
				{
					foreach (JSONObject item in npcData["TuDi"].list)
					{
						AddNpcCandidate(questNpcId, item?.Str ?? "", "徒弟", "Relation", seenIds, candidates, rejectedReasons, ref consideredCount);
					}
				}
				if (npcData.HasField("HaoYou") && npcData["HaoYou"] != null && npcData["HaoYou"].IsArray)
				{
					foreach (JSONObject item2 in npcData["HaoYou"].list)
					{
						AddNpcCandidate(questNpcId, item2?.Str ?? "", "好友", "Relation", seenIds, candidates, rejectedReasons, ref consideredCount);
						if (candidates.Count >= 5)
						{
							break;
						}
					}
				}
				if (npcData.HasField("MenPai") && npcData["MenPai"].I > 0)
				{
					int factionId = npcData["MenPai"].I;
					JSONObject avatarData = jsonData.instance?.AvatarJsonData;
					if (avatarData != null)
					{
						int count = 0;
						foreach (string key in avatarData.keys)
						{
							if (count >= 3)
							{
								break;
							}
							try
							{
								if (!avatarData.HasField(key) || avatarData[key] == null)
								{
									continue;
								}
								JSONObject other = avatarData[key];
								if (other.HasField("LiuPai") && other["LiuPai"].I == factionId)
								{
									int before = candidates.Count;
									AddNpcCandidate(questNpcId, key, "同门", "SameSect", seenIds, candidates, rejectedReasons, ref consideredCount);
									if (candidates.Count > before)
									{
										count++;
									}
								}
							}
							catch (Exception ex)
							{
								LogQuestToolWarning("PickNpcs same-sect candidate evaluation", ex);
							}
						}
					}
				}
				string currentScene = GetNpcSceneId(questNpcId);
				if (!string.IsNullOrEmpty(currentScene))
				{
					NPCMap npcMap = NpcJieSuanManager.inst?.npcMap;
					if (npcMap != null && npcMap.threeSenceNPCDictionary.TryGetValue(currentScene, out var sceneNpcs))
					{
						int count2 = 0;
						foreach (int sceneNpcId in sceneNpcs)
						{
							int before2 = candidates.Count;
							AddNpcCandidate(questNpcId, sceneNpcId.ToString(), "同场景", "SameScene", seenIds, candidates, rejectedReasons, ref consideredCount);
							if (candidates.Count > before2)
							{
								count2++;
								if (count2 >= 2)
								{
									break;
								}
							}
						}
					}
					else if (npcMap != null)
					{
						foreach (KeyValuePair<int, List<int>> kvp in npcMap.bigMapNPCDictionary)
						{
							if (!kvp.Value.Contains(questNpcId))
							{
								continue;
							}
							int count3 = 0;
							foreach (int sceneNpcId2 in kvp.Value)
							{
								int before3 = candidates.Count;
								AddNpcCandidate(questNpcId, sceneNpcId2.ToString(), "同场景", "SameScene", seenIds, candidates, rejectedReasons, ref consideredCount);
								if (candidates.Count > before3)
								{
									count3++;
									if (count3 >= 2)
									{
										break;
									}
								}
							}
							break;
						}
					}
				}
			}
			if (candidates.Count == 0)
			{
				JSONObject randomData = jsonData.instance?.AvatarRandomJsonData;
				if (randomData != null)
				{
					List<string> allNpcKeys = new List<string>(randomData.keys);
					Shuffle(allNpcKeys);
					foreach (string key2 in allNpcKeys)
					{
						AddNpcCandidate(questNpcId, key2, string.Empty, "Global", seenIds, candidates, rejectedReasons, ref consideredCount);
						if (candidates.Count >= 6)
						{
							break;
						}
					}
				}
			}
			Shuffle(candidates);
			int pickCount = Math.Min(_rng.Next(2, 4), candidates.Count);
			for (int i = 0; i < pickCount; i++)
			{
				result.Add(candidates[i]);
			}
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogWarning((object)$"[QuestElementPicker] NPC抽取异常: {arg}");
		}
		string rejectedSummary = ((rejectedReasons.Count == 0) ? "none" : string.Join(", ", from keyValuePair in rejectedReasons
			orderby keyValuePair.Key
			select $"{keyValuePair.Key}:{keyValuePair.Value}"));
		string selectedSummary = ((result.Count == 0) ? "none" : string.Join(", ", result.Select((PickedNpc npc) => npc.Id + ":" + npc.SourceBucket)));
		AIChatManager.logger.LogInfo((object)$"[QuestElementPicker] NPC候选构建 considered={consideredCount}, legalCandidates={candidates.Count}, selected={result.Count}, rejected={rejectedSummary}, selectedBuckets={selectedSummary}");
		return result;
	}

	private static void AddNpcCandidate(int publisherNpcId, string npcIdText, string relation, string sourceBucket, HashSet<string> seenIds, List<PickedNpc> candidates, Dictionary<string, int> rejectedReasons, ref int consideredCount)
	{
		if (!string.IsNullOrWhiteSpace(npcIdText))
		{
			npcIdText = npcIdText.Trim();
			consideredCount++;
			if (seenIds.Contains(npcIdText))
			{
				TrackRejectedNpcReason(rejectedReasons, "duplicate");
				return;
			}
			if (!IsSecondaryCandidateLegal(npcIdText, publisherNpcId, out var reason))
			{
				TrackRejectedNpcReason(rejectedReasons, reason);
				return;
			}
			candidates.Add(BuildPickedNpc(npcIdText, relation, sourceBucket));
			seenIds.Add(npcIdText);
		}
	}

	private static void TrackRejectedNpcReason(Dictionary<string, int> rejectedReasons, string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
		{
			reason = "unknown";
		}
		if (rejectedReasons.TryGetValue(reason, out var count))
		{
			rejectedReasons[reason] = count + 1;
		}
		else
		{
			rejectedReasons[reason] = 1;
		}
	}

	private static bool IsSecondaryCandidateLegal(string npcIdText, int publisherNpcId, out string reason)
	{
		if (!int.TryParse(npcIdText, out var npcId))
		{
			reason = "invalidId";
			return false;
		}
		return IsSecondaryCandidateLegal(npcId, publisherNpcId, out reason);
	}

	private static bool IsSecondaryCandidateLegal(int npcId, int publisherNpcId, out string reason)
	{
		string npcIdText = npcId.ToString();
		JSONObject avatarData = jsonData.instance?.AvatarJsonData;
		JSONObject randomData = jsonData.instance?.AvatarRandomJsonData;
		if (npcId < 20000)
		{
			reason = "belowNpcFloor";
			return false;
		}
		if (npcId == publisherNpcId)
		{
			reason = "publisherNpc";
			return false;
		}
		if (avatarData == null || !avatarData.HasField(npcIdText))
		{
			reason = "missingAvatarData";
			return false;
		}
		if (randomData == null || !randomData.HasField(npcIdText))
		{
			reason = "missingAvatarRandomData";
			return false;
		}
		if (string.IsNullOrWhiteSpace(randomData[npcIdText]["Name"]?.Str))
		{
			reason = "emptyName";
			return false;
		}
		if (NPCEx.IsDeath(npcId))
		{
			reason = "dead";
			return false;
		}
		if (NpcJieSuanManager.inst != null && NpcJieSuanManager.inst.IsFly(npcId))
		{
			reason = "ascended";
			return false;
		}
		if (IsImportantOrBoundNpc(npcId, avatarData[npcIdText]))
		{
			reason = "importantOrBound";
			return false;
		}
		if (IsNpcOccupied(npcId, out var occupiedReason))
		{
			reason = occupiedReason;
			return false;
		}
		reason = string.Empty;
		return true;
	}

	private static bool IsImportantOrBoundNpc(int npcId, JSONObject avatarData)
	{
		try
		{
			if (avatarData != null)
			{
				if (avatarData.HasField("isImportant") && avatarData["isImportant"].b)
				{
					return true;
				}
				if (avatarData.HasField("BindingNpcID") && avatarData["BindingNpcID"].I > 0)
				{
					return true;
				}
			}
			Dictionary<int, int> bindings = NpcJieSuanManager.inst?.ImportantNpcBangDingDictionary;
			if (bindings != null && (bindings.ContainsKey(npcId) || bindings.ContainsValue(npcId)))
			{
				return true;
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("IsImportantOrBoundNpc", ex);
		}
		return false;
	}

	private static bool IsNpcOccupied(int npcId, out string reason)
	{
		try
		{
			Avatar player = Tools.instance?.getPlayer();
			if (player?.StreamData?.TaskMag?.HasTaskNpcList != null && player.StreamData.TaskMag.HasTaskNpcList.Contains(npcId))
			{
				reason = "officialTaskBusy";
				return true;
			}
			List<int> elderTaskNpcIds = player?.ElderTaskMag?.GetExecutingTaskNpcIdList();
			if (elderTaskNpcIds != null && elderTaskNpcIds.Contains(npcId))
			{
				reason = "elderTaskBusy";
				return true;
			}
			if (KillManager.Inst != null && KillManager.Inst.RewardOrderModels != null && KillManager.Inst.RewardOrderModels.ContainsKey(npcId))
			{
				reason = "rewardOrderBusy";
				return true;
			}
			if (AIQuestManager.Instance != null && AIQuestManager.Instance.IsNpcReservedByAIQuest(npcId))
			{
				reason = "aiQuestBusy";
				return true;
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("IsNpcOccupied", ex);
		}
		reason = string.Empty;
		return false;
	}

	private static PickedNpc BuildPickedNpc(string npcIdText, string relation, string sourceBucket)
	{
		return new PickedNpc
		{
			Id = npcIdText,
			Name = GetNpcName(npcIdText),
			Relation = relation,
			Faction = GetNpcFactionName(npcIdText),
			XiuWei = GetNpcXiuWeiText(npcIdText),
			Gender = GetNpcGenderText(npcIdText),
			SourceBucket = sourceBucket
		};
	}

	public static bool IsNpcCurrentlyPresent(int npcId)
	{
		try
		{
			NPCMap npcMap = NpcJieSuanManager.inst?.npcMap;
			if (npcMap == null)
			{
				return false;
			}
			foreach (KeyValuePair<int, List<int>> item in npcMap.bigMapNPCDictionary)
			{
				if (item.Value.Contains(npcId))
				{
					return true;
				}
			}
			foreach (KeyValuePair<string, List<int>> item2 in npcMap.threeSenceNPCDictionary)
			{
				if (item2.Value.Contains(npcId))
				{
					return true;
				}
			}
			foreach (KeyValuePair<string, Dictionary<int, List<int>>> item3 in npcMap.fuBenNPCDictionary)
			{
				foreach (KeyValuePair<int, List<int>> item4 in item3.Value)
				{
					if (item4.Value.Contains(npcId))
					{
						return true;
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("IsNpcCurrentlyPresent", ex);
		}
		return false;
	}

	public static string GetNpcDisplayName(int npcId)
	{
		return GetNpcName(npcId.ToString());
	}

	private static string GetNpcXiuWeiText(string npcIdStr)
	{
		try
		{
			JSONObject randomData = jsonData.instance?.AvatarRandomJsonData;
			if (randomData != null && randomData.HasField(npcIdStr) && randomData[npcIdStr].HasField("Level"))
			{
				return MCS_Converter.GetXiuWeiText(randomData[npcIdStr]["Level"].I - 1);
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("GetNpcXiuWeiText", ex);
		}
		return string.Empty;
	}

	private static string GetNpcGenderText(string npcIdStr)
	{
		try
		{
			JSONObject randomData = jsonData.instance?.AvatarRandomJsonData;
			if (randomData != null && randomData.HasField(npcIdStr) && randomData[npcIdStr].HasField("SexType"))
			{
				return (randomData[npcIdStr]["SexType"].I == 1) ? "男" : "女";
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("GetNpcGenderText", ex);
		}
		return string.Empty;
	}

	private static int GetPlayerLevel()
	{
		try
		{
			Avatar player = Tools.instance?.getPlayer();
			if (player != null)
			{
				return player.getLevelType();
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("QuestElementPicker numeric helper", ex);
		}
		return 1;
	}

	public static string GetNpcCurrentScene(int npcId)
	{
		try
		{
			NPCMap npcMap = NpcJieSuanManager.inst?.npcMap;
			if (npcMap != null)
			{
				foreach (KeyValuePair<string, List<int>> kvp in npcMap.threeSenceNPCDictionary)
				{
					if (kvp.Value.Contains(npcId) && TryNormalizeOfficialLocationId(kvp.Key, out var sceneLocationId))
					{
						return sceneLocationId;
					}
				}
				foreach (KeyValuePair<int, List<int>> kvp2 in npcMap.bigMapNPCDictionary)
				{
					if (kvp2.Value.Contains(npcId))
					{
						string nodeLocationId = kvp2.Key.ToString();
						if (TryNormalizeOfficialLocationId(nodeLocationId, out var normalizedNodeLocationId))
						{
							return normalizedNodeLocationId;
						}
					}
				}
				foreach (KeyValuePair<string, Dictionary<int, List<int>>> kvp3 in npcMap.fuBenNPCDictionary)
				{
					foreach (KeyValuePair<int, List<int>> kvp4 in kvp3.Value)
					{
						if (kvp4.Value.Contains(npcId))
						{
							string fuBenLocationId = $"{kvp3.Key},{kvp4.Key}";
							if (TryNormalizeOfficialLocationId(fuBenLocationId, out var normalizedFuBenLocationId))
							{
								return normalizedFuBenLocationId;
							}
							if (TryNormalizeOfficialLocationId(kvp3.Key, out var normalizedFuBenSceneId))
							{
								return normalizedFuBenSceneId;
							}
						}
					}
				}
			}
			if (AIChatManager.npcInfo != null && AIChatManager.npcInfo.NpcID == npcId && TryNormalizeOfficialLocationId(SceneEx.NowSceneName, out var currentPlayerLocationId))
			{
				return currentPlayerLocationId;
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("GetNpcCurrentScene", ex);
		}
		return string.Empty;
	}

	private static string GetNpcSceneId(int npcId)
	{
		string locationId = GetNpcCurrentScene(npcId);
		if (TryParseFuBenLocationId(locationId, out var fuBenSceneId, out var _))
		{
			return fuBenSceneId;
		}
		return locationId;
	}

	public static bool IsOfficialQuestLocation(string locationId)
	{
		string normalizedLocationId;
		return TryNormalizeOfficialLocationId(locationId, out normalizedLocationId);
	}

	public static bool TryNormalizeOfficialLocationId(string rawLocationId, out string normalizedLocationId)
	{
		normalizedLocationId = string.Empty;
		string locationId = (string.IsNullOrWhiteSpace(rawLocationId) ? string.Empty : rawLocationId.Trim());
		if (string.IsNullOrEmpty(locationId))
		{
			return false;
		}
		if (SceneNameJsonData.DataDict.ContainsKey(locationId))
		{
			normalizedLocationId = locationId;
			return true;
		}
		if (int.TryParse(locationId, out var bigMapNodeId) && AllMapLuDainType.DataDict.ContainsKey(bigMapNodeId))
		{
			normalizedLocationId = bigMapNodeId.ToString();
			return true;
		}
		if (TryParseFuBenLocationId(locationId, out var fuBenSceneId, out var fuBenNodeIndex))
		{
			normalizedLocationId = ((fuBenNodeIndex > 0) ? $"{fuBenSceneId},{fuBenNodeIndex}" : fuBenSceneId);
			return true;
		}
		return false;
	}

	public static bool TryParseFuBenLocationId(string rawLocationId, out string fuBenSceneId, out int fuBenNodeIndex)
	{
		fuBenSceneId = string.Empty;
		fuBenNodeIndex = 0;
		string locationId = (string.IsNullOrWhiteSpace(rawLocationId) ? string.Empty : rawLocationId.Trim());
		if (string.IsNullOrEmpty(locationId) || !locationId.StartsWith("F", StringComparison.Ordinal))
		{
			return false;
		}
		string[] parts = locationId.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0 || parts.Length > 2)
		{
			return false;
		}
		string sceneId = parts[0].Trim();
		if (!SceneNameJsonData.DataDict.ContainsKey(sceneId))
		{
			return false;
		}
		fuBenSceneId = sceneId;
		if (parts.Length == 1)
		{
			return true;
		}
		return int.TryParse(parts[1].Trim(), out fuBenNodeIndex) && fuBenNodeIndex > 0;
	}

	public static bool TryResolveOfficialLocationId(string rawLocationText, out string locationId)
	{
		locationId = string.Empty;
		if (TryNormalizeOfficialLocationId(rawLocationText, out var normalizedLocationId))
		{
			locationId = normalizedLocationId;
			return true;
		}
		string locationText = (string.IsNullOrWhiteSpace(rawLocationText) ? string.Empty : rawLocationText.Trim());
		if (string.IsNullOrEmpty(locationText))
		{
			return false;
		}
		string normalizedLocationText = NormalizeNarrativeEntityText(locationText);
		List<string> matchedIds = (from kvp in GetOfficialLocationNameLookup()
			where NarrativeEntityTextEquals(kvp.Key, normalizedLocationText)
			select kvp).SelectMany((KeyValuePair<string, List<string>> kvp) => kvp.Value).Distinct(StringComparer.Ordinal).Take(2)
			.ToList();
		if (matchedIds.Count != 1)
		{
			return false;
		}
		locationId = matchedIds[0];
		return true;
	}

	internal static bool TryGetOfficialLocationName(string rawLocationId, out string locationName)
	{
		locationName = string.Empty;
		if (!TryNormalizeOfficialLocationId(rawLocationId, out var normalizedLocationId))
		{
			return false;
		}
		Dictionary<string, PickedLocation> officialPool = BuildOfficialLocationPool();
		if (!officialPool.TryGetValue(normalizedLocationId, out var location) || string.IsNullOrWhiteSpace(location?.Name))
		{
			return false;
		}
		locationName = location.Name.Trim();
		return true;
	}

	internal static string NormalizeNarrativeEntityText(string rawText)
	{
		string normalized = rawText?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return string.Empty;
		}
		(char, char)[] wrapperPairs = new(char, char)[9]
		{
			('《', '》'),
			('〈', '〉'),
			('「', '」'),
			('『', '』'),
			('【', '】'),
			('“', '”'),
			('‘', '’'),
			('"', '"'),
			('\'', '\'')
		};
		bool stripped;
		do
		{
			stripped = false;
			(char, char)[] array = wrapperPairs;
			for (int i = 0; i < array.Length; i++)
			{
				var (open, close) = array[i];
				if (normalized.Length >= 2 && normalized[0] == open && normalized[normalized.Length - 1] == close)
				{
					normalized = normalized.Substring(1, normalized.Length - 2).Trim();
					stripped = true;
					break;
				}
			}
		}
		while (stripped && normalized.Length >= 2);
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return string.Empty;
		}
		StringBuilder builder = new StringBuilder(normalized.Length);
		string text = normalized;
		foreach (char ch in text)
		{
			if (!char.IsWhiteSpace(ch))
			{
				builder.Append(ch);
			}
		}
		return builder.ToString();
	}

	internal static bool NarrativeEntityTextEquals(string left, string right)
	{
		string normalizedLeft = NormalizeNarrativeEntityText(left);
		string normalizedRight = NormalizeNarrativeEntityText(right);
		return !string.IsNullOrWhiteSpace(normalizedLeft) && !string.IsNullOrWhiteSpace(normalizedRight) && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
	}

	internal static bool NarrativeEntityTextContains(string container, string content)
	{
		string normalizedContainer = NormalizeNarrativeEntityText(container);
		string normalizedContent = NormalizeNarrativeEntityText(content);
		return !string.IsNullOrWhiteSpace(normalizedContainer) && !string.IsNullOrWhiteSpace(normalizedContent) && normalizedContainer.IndexOf(normalizedContent, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static Dictionary<string, PickedLocation> BuildOfficialLocationPool()
	{
		if (_officialLocationPoolCache != null)
		{
			return _officialLocationPoolCache;
		}
		lock (_officialLocationCacheLock)
		{
			if (_officialLocationPoolCache != null)
			{
				return _officialLocationPoolCache;
			}
			Dictionary<string, PickedLocation> locations = new Dictionary<string, PickedLocation>(StringComparer.Ordinal);
			try
			{
				foreach (NTaskSuiJI officialTaskLocation in NTaskSuiJI.DataList)
				{
					TryAddOfficialLocation(locations, officialTaskLocation?.StrValue, officialTaskLocation?.name);
				}
			}
			catch (Exception ex)
			{
				LogQuestToolWarning("BuildOfficialLocationPool NTaskSuiJI", ex);
			}
			try
			{
				foreach (NpcBigMapBingDate bigMapBinding in NpcBigMapBingDate.DataList)
				{
					TryAddOfficialLocation(locations, bigMapBinding.MapD.ToString(), null);
				}
			}
			catch (Exception ex2)
			{
				LogQuestToolWarning("BuildOfficialLocationPool NpcBigMapBingDate", ex2);
			}
			try
			{
				foreach (NpcThreeMapBingDate threeSceneBinding in NpcThreeMapBingDate.DataList)
				{
					AddThreeSceneLocations(locations, threeSceneBinding);
				}
			}
			catch (Exception ex3)
			{
				LogQuestToolWarning("BuildOfficialLocationPool NpcThreeMapBingDate", ex3);
			}
			try
			{
				foreach (NpcFuBenMapBingDate fuBenBinding in NpcFuBenMapBingDate.DataList)
				{
					AddFuBenLocations(locations, fuBenBinding);
				}
			}
			catch (Exception ex4)
			{
				LogQuestToolWarning("BuildOfficialLocationPool NpcFuBenMapBingDate", ex4);
			}
			_officialLocationPoolCache = locations;
			_officialLocationNameLookupCache = BuildOfficialLocationNameLookup(locations);
			_officialLocationPoolBuildCountForTest++;
			return _officialLocationPoolCache;
		}
	}

	private static Dictionary<string, List<string>> GetOfficialLocationNameLookup()
	{
		if (_officialLocationNameLookupCache != null)
		{
			return _officialLocationNameLookupCache;
		}
		BuildOfficialLocationPool();
		return _officialLocationNameLookupCache ?? new Dictionary<string, List<string>>(StringComparer.Ordinal);
	}

	private static Dictionary<string, List<string>> BuildOfficialLocationNameLookup(Dictionary<string, PickedLocation> locations)
	{
		Dictionary<string, List<string>> nameLookup = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		if (locations == null)
		{
			return nameLookup;
		}
		foreach (PickedLocation location in locations.Values)
		{
			if (location != null && !string.IsNullOrWhiteSpace(location.Name) && !string.IsNullOrWhiteSpace(location.Id))
			{
				string key = location.Name.Trim();
				if (!nameLookup.TryGetValue(key, out var ids))
				{
					ids = (nameLookup[key] = new List<string>());
				}
				if (!ids.Contains(location.Id))
				{
					ids.Add(location.Id);
				}
			}
		}
		return nameLookup;
	}

	internal static void ResetOfficialLocationLookupCacheForTest()
	{
		lock (_officialLocationCacheLock)
		{
			_officialLocationPoolCache = null;
			_officialLocationNameLookupCache = null;
			_officialLocationPoolBuildCountForTest = 0;
		}
	}

	internal static int GetOfficialLocationPoolBuildCountForTest()
	{
		return _officialLocationPoolBuildCountForTest;
	}

	internal static string GetUniqueOfficialLocationNameForTest()
	{
		return (from kvp in GetOfficialLocationNameLookup()
			where kvp.Value != null && kvp.Value.Count == 1 && !string.IsNullOrWhiteSpace(kvp.Key)
			select kvp.Key).FirstOrDefault();
	}

	private static List<string> GetNpcPreferredLocationIds(int npcId)
	{
		List<string> preferredLocationIds = new List<string>();
		int npcType = GetNpcType(npcId);
		if (npcType <= 0)
		{
			return preferredLocationIds;
		}
		try
		{
			foreach (NpcBigMapBingDate bigMapBinding in NpcBigMapBingDate.DataList)
			{
				if (bigMapBinding.MapType == 1 || (bigMapBinding.MapType == 2 && bigMapBinding.NPCType == npcType))
				{
					preferredLocationIds.Add(bigMapBinding.MapD.ToString());
				}
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning($"GetNpcPreferredLocationIds bigMap npcId={npcId}", ex);
		}
		try
		{
			if (NpcThreeMapBingDate.DataDict.TryGetValue(npcType, out var threeSceneBinding))
			{
				preferredLocationIds.AddRange(GetThreeSceneLocationIds(threeSceneBinding));
			}
		}
		catch (Exception ex2)
		{
			LogQuestToolWarning($"GetNpcPreferredLocationIds threeScene npcId={npcId}", ex2);
		}
		try
		{
			if (NpcFuBenMapBingDate.DataDict.TryGetValue(npcType, out var fuBenBinding))
			{
				preferredLocationIds.AddRange(GetFuBenLocationIds(fuBenBinding));
			}
		}
		catch (Exception ex3)
		{
			LogQuestToolWarning($"GetNpcPreferredLocationIds fuBen npcId={npcId}", ex3);
		}
		string normalizedLocationId;
		return preferredLocationIds.Where((string id) => TryNormalizeOfficialLocationId(id, out normalizedLocationId)).Distinct(StringComparer.Ordinal).ToList();
	}

	private static int GetNpcType(int npcId)
	{
		try
		{
			if (jsonData.instance?.AvatarJsonData != null && jsonData.instance.AvatarJsonData.HasField(npcId.ToString()))
			{
				return jsonData.instance.AvatarJsonData[npcId.ToString()]["Type"].I;
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning($"GetNpcType npcId={npcId}", ex);
		}
		return 0;
	}

	private static List<string> GetThreeSceneLocationIds(NpcThreeMapBingDate binding)
	{
		List<string> locationIds = new List<string>();
		if (binding == null)
		{
			return locationIds;
		}
		FieldInfo[] fields = typeof(NpcThreeMapBingDate).GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo field in fields)
		{
			if (field.FieldType != typeof(List<int>) || !(field.GetValue(binding) is List<int> sceneIds))
			{
				continue;
			}
			foreach (int sceneId in sceneIds)
			{
				locationIds.Add($"S{sceneId}");
			}
		}
		return locationIds;
	}

	private static List<string> GetFuBenLocationIds(NpcFuBenMapBingDate binding)
	{
		List<string> locationIds = new List<string>();
		if (binding == null)
		{
			return locationIds;
		}
		AddFuBenLocationIds(locationIds, binding.CaiJi, binding.CaiJiDian);
		AddFuBenLocationIds(locationIds, binding.CaiKuang, binding.CaiKuangDian);
		AddFuBenLocationIds(locationIds, binding.XunLuo, binding.XunLuoDian);
		AddFuBenLocationIds(locationIds, binding.LingHe, binding.LingHeDian1);
		AddFuBenLocationIds(locationIds, binding.LingHe, binding.LingHeDian2);
		AddFuBenLocationIds(locationIds, binding.LingHe, binding.LingHeDian3);
		AddFuBenLocationIds(locationIds, binding.LingHe, binding.LingHeDian4);
		AddFuBenLocationIds(locationIds, binding.LingHe, binding.LingHeDian5);
		AddFuBenLocationIds(locationIds, binding.LingHe, binding.LingHeDian6);
		return locationIds;
	}

	private static void AddThreeSceneLocations(Dictionary<string, PickedLocation> locations, NpcThreeMapBingDate binding)
	{
		foreach (string locationId in GetThreeSceneLocationIds(binding))
		{
			TryAddOfficialLocation(locations, locationId, null);
		}
	}

	private static void AddFuBenLocations(Dictionary<string, PickedLocation> locations, NpcFuBenMapBingDate binding)
	{
		foreach (string locationId in GetFuBenLocationIds(binding))
		{
			TryAddOfficialLocation(locations, locationId, null);
		}
	}

	private static void AddFuBenLocationIds(List<string> locationIds, int sceneNumber, List<int> nodeIds)
	{
		if (sceneNumber <= 0 || nodeIds == null)
		{
			return;
		}
		string sceneId = $"F{sceneNumber}";
		foreach (int nodeId in nodeIds)
		{
			if (nodeId > 0)
			{
				locationIds.Add($"{sceneId},{nodeId}");
			}
		}
	}

	private static bool TryAddPickedLocation(List<PickedLocation> result, HashSet<string> pickedIds, HashSet<string> recentLocationIds, Dictionary<string, PickedLocation> officialPool, string locationId)
	{
		if (!TryNormalizeOfficialLocationId(locationId, out var normalizedLocationId))
		{
			return false;
		}
		if (pickedIds.Contains(normalizedLocationId) || recentLocationIds.Contains(normalizedLocationId))
		{
			return false;
		}
		if (!officialPool.TryGetValue(normalizedLocationId, out var pickedLocation) && !TryBuildOfficialPickedLocation(normalizedLocationId, null, out pickedLocation))
		{
			return false;
		}
		result.Add(pickedLocation);
		pickedIds.Add(normalizedLocationId);
		return true;
	}

	private static bool TryAddOfficialLocation(Dictionary<string, PickedLocation> locations, string locationId, string preferredName)
	{
		if (!TryBuildOfficialPickedLocation(locationId, preferredName, out var pickedLocation))
		{
			return false;
		}
		if (locations.TryGetValue(pickedLocation.Id, out var existingLocation))
		{
			if ((string.IsNullOrWhiteSpace(existingLocation.Name) || existingLocation.Name == existingLocation.Id) && !string.IsNullOrWhiteSpace(pickedLocation.Name))
			{
				locations[pickedLocation.Id] = pickedLocation;
			}
			return false;
		}
		locations.Add(pickedLocation.Id, pickedLocation);
		return true;
	}

	private static bool TryBuildOfficialPickedLocation(string locationId, string preferredName, out PickedLocation pickedLocation)
	{
		pickedLocation = null;
		if (!TryNormalizeOfficialLocationId(locationId, out var normalizedLocationId))
		{
			return false;
		}
		pickedLocation = new PickedLocation
		{
			Id = normalizedLocationId,
			Name = GetOfficialLocationDisplayName(normalizedLocationId, preferredName),
			RegionName = GetOfficialLocationRegionName(normalizedLocationId)
		};
		return true;
	}

	private static string GetOfficialLocationDisplayName(string locationId, string preferredName = null)
	{
		string explicitName = (string.IsNullOrWhiteSpace(preferredName) ? string.Empty : preferredName.Trim());
		if (!string.IsNullOrEmpty(explicitName))
		{
			return explicitName;
		}
		foreach (NTaskSuiJI officialTaskLocation in NTaskSuiJI.DataList)
		{
			if (officialTaskLocation != null && officialTaskLocation.StrValue == locationId && !string.IsNullOrWhiteSpace(officialTaskLocation.name))
			{
				return officialTaskLocation.name.Trim();
			}
		}
		foreach (MapIndexData mapIndexData in MapIndexData.DataList)
		{
			if (mapIndexData != null && mapIndexData.StrValue == locationId && !string.IsNullOrWhiteSpace(mapIndexData.name))
			{
				return mapIndexData.name.Trim();
			}
		}
		if (TryParseFuBenLocationId(locationId, out var fuBenSceneId, out var fuBenNodeIndex))
		{
			string sceneName = GetSceneDisplayName(fuBenSceneId);
			return (fuBenNodeIndex > 0) ? $"{sceneName}·{fuBenNodeIndex}号点位" : sceneName;
		}
		if (SceneNameJsonData.DataDict.TryGetValue(locationId, out var scene))
		{
			return GetSceneDisplayName(scene.id);
		}
		if (int.TryParse(locationId, out var bigMapNodeId) && AllMapLuDainType.DataDict.TryGetValue(bigMapNodeId, out var bigMapNode))
		{
			return string.IsNullOrWhiteSpace(bigMapNode.LuDianName) ? locationId : bigMapNode.LuDianName.Trim();
		}
		return locationId;
	}

	private static string GetOfficialLocationRegionName(string locationId)
	{
		if (TryParseFuBenLocationId(locationId, out var fuBenSceneId, out var _))
		{
			locationId = fuBenSceneId;
		}
		if (SceneNameJsonData.DataDict.TryGetValue(locationId, out var scene))
		{
			return scene.MapName ?? string.Empty;
		}
		if (int.TryParse(locationId, out var bigMapNodeId) && AllMapLuDainType.DataDict.TryGetValue(bigMapNodeId, out var bigMapNode))
		{
			return (bigMapNode.MapType == 2) ? "海外" : "宁州";
		}
		return string.Empty;
	}

	private static string GetSceneDisplayName(string sceneId)
	{
		if (!SceneNameJsonData.DataDict.TryGetValue(sceneId, out var scene))
		{
			return sceneId;
		}
		if (!string.IsNullOrWhiteSpace(scene.EventName))
		{
			return scene.EventName.Trim();
		}
		if (!string.IsNullOrWhiteSpace(scene.MapName))
		{
			return scene.MapName.Trim();
		}
		return sceneId;
	}

	public static string BuildSceneHierarchy(string sceneId)
	{
		try
		{
			if (TryNormalizeOfficialLocationId(sceneId, out var normalizedLocationId))
			{
				return GetOfficialLocationDisplayName(normalizedLocationId);
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("BuildSceneHierarchy", ex);
		}
		return string.IsNullOrWhiteSpace(sceneId) ? "未知" : sceneId.Trim();
	}

	public static bool IsConcreteQuestScene(string sceneId)
	{
		return IsOfficialQuestLocation(sceneId);
	}

	private static string GetNpcName(string npcIdStr)
	{
		try
		{
			JSONObject randomData = jsonData.instance?.AvatarRandomJsonData;
			if (randomData != null && randomData.HasField(npcIdStr))
			{
				return randomData[npcIdStr]["Name"]?.Str ?? ("NPC_" + npcIdStr);
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("QuestElementPicker npc name helper", ex);
		}
		return "NPC_" + npcIdStr;
	}

	private static string GetItemCategoryName(int type)
	{
		return type switch
		{
			1 => "武器",
			2 => "防具",
			3 => "丹药",
			4 => "材料",
			5 => "食物",
			_ => "杂物",
		};
	}

	private static string TruncateString(string s, int maxLen)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		return (s.Length <= maxLen) ? s : (s.Substring(0, maxLen) + "...");
	}

	private static HashSet<int> GetRecentItemIds(QuestProgramState programState)
	{
		HashSet<int> ids = new HashSet<int>();
		if (programState?.Programs == null)
		{
			return ids;
		}
		foreach (QuestProgram program in programState.Programs.Where((QuestProgram questProgram) => questProgram != null).Reverse().Take(3))
		{
			IEnumerable<QuestNode> nodes = program.Nodes;
			foreach (QuestNode node in nodes ?? Enumerable.Empty<QuestNode>())
			{
				CollectRecentItemIds(node?.ActivationPredicates, ids);
				CollectRecentItemIds(node?.CompletionPredicates, ids);
				CollectRecentItemIds(node?.Narrative?.Gates, ids);
				CollectRecentItemIds(node?.OnActivated, ids);
				CollectRecentItemIds(node?.OnCompleted, ids);
				IEnumerable<QuestChoiceOption> enumerable = node?.Narrative?.Choices;
				foreach (QuestChoiceOption choice in enumerable ?? Enumerable.Empty<QuestChoiceOption>())
				{
					CollectRecentItemIds(choice.Requirements, ids);
					CollectRecentItemIds(choice.Consequences, ids);
				}
			}
		}
		return ids;
	}

	private static void CollectRecentItemIds(IEnumerable<QuestPredicateInvocation> predicates, HashSet<int> ids)
	{
		if (predicates == null)
		{
			return;
		}
		foreach (QuestPredicateInvocation predicate in predicates)
		{
			if (predicate != null)
			{
				if (string.Equals(predicate.Opcode, "player.has_item", StringComparison.OrdinalIgnoreCase) && predicate.Params != null && predicate.Params.TryGetValue("itemId", out var itemIdText) && int.TryParse(itemIdText, out var itemId))
				{
					ids.Add(itemId);
				}
				CollectRecentItemIds(predicate.Children, ids);
			}
		}
	}

	private static void CollectRecentItemIds(IEnumerable<QuestCommandInvocation> commands, HashSet<int> ids)
	{
		if (commands == null)
		{
			return;
		}
		foreach (QuestCommandInvocation command in commands)
		{
			if (command?.Params != null && (string.Equals(command.Opcode, "player.add_item", StringComparison.OrdinalIgnoreCase) || string.Equals(command.Opcode, "mail.delivery_submitted", StringComparison.OrdinalIgnoreCase)) && command.Params.TryGetValue("itemId", out var itemIdText) && int.TryParse(itemIdText, out var itemId))
			{
				ids.Add(itemId);
			}
		}
	}

	private static HashSet<string> GetRecentSceneIds(QuestProgramState programState)
	{
		HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
		if (programState?.Programs == null)
		{
			return ids;
		}
		foreach (QuestProgram program in programState.Programs.Where((QuestProgram questProgram) => questProgram != null).Reverse().Take(3))
		{
			IEnumerable<QuestNode> nodes = program.Nodes;
			foreach (QuestNode node in nodes ?? Enumerable.Empty<QuestNode>())
			{
				CollectRecentSceneIds(node?.ActivationPredicates, ids);
				CollectRecentSceneIds(node?.CompletionPredicates, ids);
				CollectRecentSceneIds(node?.Narrative?.Gates, ids);
				CollectRecentSceneIds(node?.OnActivated, ids);
				CollectRecentSceneIds(node?.OnCompleted, ids);
				IEnumerable<QuestChoiceOption> enumerable = node?.Narrative?.Choices;
				foreach (QuestChoiceOption choice in enumerable ?? Enumerable.Empty<QuestChoiceOption>())
				{
					CollectRecentSceneIds(choice.Requirements, ids);
					CollectRecentSceneIds(choice.Consequences, ids);
				}
			}
		}
		return ids;
	}

	private static void CollectRecentSceneIds(IEnumerable<QuestPredicateInvocation> predicates, HashSet<string> ids)
	{
		if (predicates == null)
		{
			return;
		}
		foreach (QuestPredicateInvocation predicate in predicates)
		{
			if (predicate != null)
			{
				if (string.Equals(predicate.Opcode, "scene.is", StringComparison.OrdinalIgnoreCase) && TryGetNormalizedRecentSceneId(predicate.Params, out var sceneId))
				{
					ids.Add(sceneId);
				}
				CollectRecentSceneIds(predicate.Children, ids);
			}
		}
	}

	private static void CollectRecentSceneIds(IEnumerable<QuestCommandInvocation> commands, HashSet<string> ids)
	{
		if (commands == null)
		{
			return;
		}
		foreach (QuestCommandInvocation command in commands)
		{
			if (command != null && string.Equals(command.Opcode, "scene.load", StringComparison.OrdinalIgnoreCase) && TryGetNormalizedRecentSceneId(command.Params, out var sceneId))
			{
				ids.Add(sceneId);
			}
		}
	}

	private static bool TryGetNormalizedRecentSceneId(Dictionary<string, string> parameters, out string sceneId)
	{
		sceneId = string.Empty;
		if (parameters == null)
		{
			return false;
		}
		if (parameters.TryGetValue("sceneId", out var sceneIdText) && TryNormalizeOfficialLocationId(sceneIdText, out var normalizedSceneId))
		{
			sceneId = normalizedSceneId;
			return true;
		}
		if (parameters.TryGetValue("sceneName", out var sceneNameText) && TryResolveOfficialLocationId(sceneNameText, out var resolvedSceneId))
		{
			sceneId = resolvedSceneId;
			return true;
		}
		return false;
	}

	private static void Shuffle<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = _rng.Next(i + 1);
			T tmp = list[i];
			list[i] = list[j];
			list[j] = tmp;
		}
	}

	private static string GetNpcFactionName(string npcIdStr)
	{
		try
		{
			JSONObject randomData = jsonData.instance?.AvatarRandomJsonData;
			if (randomData != null && randomData.HasField(npcIdStr))
			{
				JSONObject npc = randomData[npcIdStr];
				if (npc.HasField("MenPai") && npc["MenPai"].I > 0)
				{
					return Tools.getStr("menpai" + npc["MenPai"].I);
				}
			}
		}
		catch (Exception ex)
		{
			LogQuestToolWarning("QuestElementPicker faction helper", ex);
		}
		return "散修";
	}
}
