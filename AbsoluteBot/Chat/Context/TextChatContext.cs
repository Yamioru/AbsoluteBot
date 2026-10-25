using System.Collections.Generic;
using AbsoluteBot.Models;
using AbsoluteBot.Services.ChatServices.Interfaces;
using AbsoluteBot.Services.UtilityServices;

namespace AbsoluteBot.Chat.Context;

public class TextChatContext(string username, int maxMessageLength, IChatService chatService,
    List<string>? lastMessages, ReplyInfo? replyInfo)
    : ChatContext("WebText", username, maxMessageLength, chatService, lastMessages, replyInfo,
        new CommonTextFormatter());

