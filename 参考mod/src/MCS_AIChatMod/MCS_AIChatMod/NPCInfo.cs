using System.Collections.Generic;

namespace MCS_AIChatMod;

public class NPCInfo
{
	public List<UINPCEventData> Events = new List<UINPCEventData>();

	public int NpcID { get; set; }

	public string Name { get; set; }

	public int Favor { get; set; }

	public string HaoGanDu { get; set; }

	public int QingFen { get; set; }

	public int Level { get; set; }

	public string XiuWei { get; set; }

	public int Status { get; set; }

	public string StatusStr { get; set; }

	public int ShouYuan { get; set; }

	public int Age { get; set; }

	public int ZiZhi { get; set; }

	public int WuXing { get; set; }

	public int DunSu { get; set; }

	public int ShenShi { get; set; }

	public string XingGe { get; set; }

	public string ZhengXie { get; set; }

	public int HP { get; set; }

	public string Gender { get; set; }

	public string Title { get; set; }

	public List<string> BackPackNames { get; set; } = new List<string>();

	public List<string> StaticSkillsNames { get; set; } = new List<string>();

	public List<string> SkillsNames { get; set; } = new List<string>();
}
