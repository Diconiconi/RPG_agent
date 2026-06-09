using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineJsonFieldContract
{
	public string Name;

	public string Type;

	public bool Required;

	public bool Multiple;

	public bool Unique;

	public List<string> AllowedValues = new List<string>();

	public List<string> RequiredWhen = new List<string>();

	public List<string> ForbiddenWhen = new List<string>();

	public string ContentRequirement;

	public string DefaultWhenOmitted;

	public List<MainlineJsonFieldContract> FieldContracts = new List<MainlineJsonFieldContract>();

	public MainlineJsonFieldContract ItemContract;
}
