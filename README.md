# twitch-dril-bot
Arguably my most useless invention yet.

This is a bot that emulates https://twitter.com/dril in your Twitch chat.

Compile it in Visual Studio 2017, replace YOUR_NAME_HERE and YOUR_AUTH_TOKEN_HERE with whatever credentials work for the account you want to run this as and replace YOUR_CHANNELS_HERE with a comma-separated list of chats you want it to reside in (must start with a #). If you don't know what these are, read https://help.twitch.tv/customer/portal/articles/1302780-twitch-irc

Has a 5 second overall cooldown time and I think a 1 minute cooldown time per user, I forget.

Once a day (or at launch) it fetches any new tweets from an archive. It saves full tweets and word pair relationships. I just had it ignore tweets containing some words that I didn't want randomly showing up in my chat, you can delete those clauses if you want

Commands:
- !dril - recites an intact tweet from dril's history
- !ebooks - constructs a nonsensical tweet based on dril's word usage patterns

please do not use this bot

I based the IRC structure off of http://emudevs.com/showthread.php/1446-C-IRC-Bot-Skeleton 