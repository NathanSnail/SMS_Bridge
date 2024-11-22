using System.Text.Json;
using SMS_Bridge.Models;
using SMS_Bridge.Services;

namespace SMS_Bridge.SmsProviders
{
	public class ETxtSmsProvider : ISmsProvider
	{
		private string key;
		private string secret;
		private static readonly string etxt_base = "http://api.etxtservice.co.nz/";
		private static readonly HttpClient client = new HttpClient();

		private static string B64Encode(string plainText)
		{
			var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
			return System.Convert.ToBase64String(bytes);
		}
		public string AuthHeader()
		{
			return "Basic " + B64Encode(key + ":" + secret);
		}
		public ETxtSmsProvider(string apiKey, string apiSecret)
		{
			key = apiKey;
			secret = apiSecret;
			client.DefaultRequestHeaders.Add("Authorization", AuthHeader());
		}
		private IResult ResultFromMessage(HttpResponseMessage message)
		{
			if (message.IsSuccessStatusCode)
			{
				return Results.Ok();
			}
			Logger.LogError("eTXTSmsProvider", "SendingError", "", message.ToString());
			return Results.Problem(detail: message.ToString(), statusCode: 501);
		}
		public async Task<(IResult Result, Guid MessageId)> SendSms(SendSmsRequest request)
		{
			var message =
			$$"""
			{
				"messages": [
					{
						"content": "{{System.Web.HttpUtility.JavaScriptStringEncode(request.Message)}}",
						"destination_number": "{{System.Web.HttpUtility.JavaScriptStringEncode(request.PhoneNumber)}}"
					}
				]
			}
			""";
			var content = new StringContent(message, System.Text.Encoding.Default, "application/json");
			var message_result = await client.PostAsync(etxt_base + "v1/messages", content);
			var doc = JsonDocument.Parse(await message_result.Content.ReadAsStringAsync());
			var guid = Guid.Empty;
			try
			{
				var message_id = doc.RootElement.GetProperty("messages").EnumerateArray().Aggregate((a, b) => a).GetProperty("message_id").GetString();
				Console.WriteLine(message_id);
				if (message_id == null) throw new Exception();
				guid = new Guid(message_id);
			}
			catch (Exception) { }
			Console.WriteLine(guid);
			return (ResultFromMessage(message_result), guid);
		}

		// the api takes a really long time to resolve to delivered, it's enroute pretty fast though.
		public async Task<SmsStatus> GetMessageStatus(Guid messageId)
		{
			var response = await client.GetAsync(etxt_base + "v1/messages/" + messageId);
			if (response == null)
			{
				Logger.LogWarning(provider: "eTXT", eventType: "StatusError", messageID: messageId.ToString(), details: "Message failed for an unknown reason");
				return SmsStatus.TimedOut;
			}
			try
			{
				response.EnsureSuccessStatusCode();
			}
			catch (Exception)
			{
				Logger.LogWarning(provider: "eTXT", eventType: "StatusError", messageID: messageId.ToString(), details: "Message failed for an unknown reason");
				return SmsStatus.TimedOut;
			}
			var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			var status = doc.RootElement.GetProperty("status").GetString();
			switch (status)
			{
				case "submitted":
				case "enroute":
					return SmsStatus.Pending;
				case "rejected":
				case "failed":
					return SmsStatus.Failed;
				case "delivered":
					return SmsStatus.Delivered;
				case "expired":
					return SmsStatus.Unknown;
			}
			Logger.LogError(provider: "eTXT", eventType: "StatusError", messageID: messageId.ToString(), details: $"Message status {status} is not in the api documentation");
			return SmsStatus.Unknown;
		}

		public Task<IEnumerable<ReceiveSmsRequest>> GetReceivedMessages()
		{
			Logger.LogWarning(
				provider: "eTXT",
				eventType: "NotImplemented",
				messageID: "",
				details: "Receive Messages attempted but eTXT provider is not implemented"
			);
			return Task.FromResult(Enumerable.Empty<ReceiveSmsRequest>());
		}
		public Task<DeleteMessageResponse> DeleteReceivedMessage(Guid messageId)
		{
			Logger.LogWarning(
				provider: "eTXT",
				eventType: "NotImplemented",
				messageID: "",
				details: "Delete Message attempted but eTXT provider is not implemented"
			);
			return Task.FromResult(new DeleteMessageResponse(
				MessageID: messageId.ToString(),
				Deleted: false,
				DeleteFeedback: "Delete operation not implemented for eTXT provider"
			));
		}

		public Task<IEnumerable<MessageStatusRecord>> GetRecentMessageStatuses()
		{
			Logger.LogWarning(
				provider: "eTXT",
				eventType: "NotImplemented",
				messageID: "",
				details: "Get Recent Statuses attempted but eTXT provider is not implemented"
			);
			return Task.FromResult(Enumerable.Empty<MessageStatusRecord>());
		}


	}
}
