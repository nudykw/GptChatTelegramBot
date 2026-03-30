import re
with open('/Users/nudyk/Projects/DotNet/GPTChatTelegramBot/ServiceLayer/Services/Telegram/UpdateHandler.cs', 'r') as f:
    text = f.read()

# carefully replace
text = text.replace('private static User? _botInfo;', 'private static TgUser? _botInfo;')
text = text.replace('(Message message,', '(TgMessage message,')
text = text.replace('User? user = message.From;', 'TgUser? user = message.From;')
text = text.replace('Message sentMessage = await action;', 'TgMessage sentMessage = await action;')
text = text.replace(', Message message,', ', TgMessage message,')
text = text.replace('Task<Message> ', 'Task<TgMessage> ')
text = text.replace('Message result = ', 'TgMessage result = ')
text = text.replace('            Message message)', '            TgMessage message)')
text = text.replace('Chat chat, User? user', 'Chat chat, TgUser? user')
text = text.replace('User? user, Chat chat', 'TgUser? user, Chat chat')

# any other Task<Message>? -> Task<TgMessage>
text = text.replace('Task<Message>', 'Task<TgMessage>')

with open('/Users/nudyk/Projects/DotNet/GPTChatTelegramBot/ServiceLayer/Services/Telegram/UpdateHandler.cs', 'w') as f:
    f.write(text)
