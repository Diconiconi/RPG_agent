using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MCS_AIChatMod;

internal static class EmbeddedDonateAssets
{
	public const string WechatResourceName = "MCS_AIChatMod.DonateAssets.wechatpay.qr";

	public const string AlipayResourceName = "MCS_AIChatMod.DonateAssets.alipay.qr";

	private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("MCS_AIChatMod::DonateQR::v1");

	private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

	public static Sprite LoadSprite(string resourceName)
	{
		if (string.IsNullOrEmpty(resourceName))
		{
			return null;
		}
		if (SpriteCache.TryGetValue(resourceName, out var cachedSprite))
		{
			return cachedSprite;
		}
		try
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
			if (stream == null)
			{
				AIChatManager.logger.LogWarning((object)("[EmbeddedDonateAssets] Missing resource: " + resourceName));
				return null;
			}
			byte[] encryptedBytes;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				stream.CopyTo(memoryStream);
				encryptedBytes = memoryStream.ToArray();
			}
			byte[] imageBytes = Decrypt(encryptedBytes);
			Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, mipChain: false);
			if (!texture.LoadImage(imageBytes))
			{
				UnityEngine.Object.Destroy(texture);
				AIChatManager.logger.LogWarning((object)("[EmbeddedDonateAssets] Failed to decode resource: " + resourceName));
				return null;
			}
			texture.name = resourceName;
			Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
			sprite.name = resourceName;
			SpriteCache[resourceName] = sprite;
			return sprite;
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogWarning((object)("[EmbeddedDonateAssets] Failed to load " + resourceName + ": " + ex.Message));
			return null;
		}
	}

	private static byte[] Decrypt(byte[] encryptedBytes)
	{
		byte[] output = new byte[encryptedBytes.Length];
		for (int i = 0; i < encryptedBytes.Length; i++)
		{
			output[i] = (byte)(encryptedBytes[i] ^ EncryptionKey[i % EncryptionKey.Length]);
		}
		return output;
	}
}
