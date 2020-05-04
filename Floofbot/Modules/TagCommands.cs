﻿using Discord;
using Discord.Commands;
using Floofbot.Services.Repository;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tag = Floofbot.Services.Repository.Models.Tag;

namespace Floofbot.Modules
{
    [Group("tag")]
    [Summary("Tag commands")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(Discord.GuildPermission.AttachFiles)]
    public class TagCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Discord.Color EMBED_COLOR = Color.Magenta;
        private static readonly int TAGS_PER_PAGE = 50;
        private static readonly List<string> SUPPORTED_IMAGE_EXTENSIONS = new List<string>
        {
            "jpg", "png", "jpeg", "webp", "gifv", "gif", "mp4"
        };

        private FloofDataContext _floofDb;

        public TagCommands(FloofDataContext floofDb)
        {
            _floofDb = floofDb;
        }

        [Command("add")]
        [Priority(0)]
        [RequireUserPermission(GuildPermission.AttachFiles)]
        public async Task Add(
            [Summary("Tag name")] string tag,
            [Summary("Tag content")] [Remainder] string content = null)
        {
            if (content != null)
            {
                Regex rgx = new Regex("[^a-zA-Z0-9 -]");
                tag = rgx.Replace(tag, "").ToLower();
                string tagName = $"{tag.ToString()}:{Context.Guild.Id}";

                try
                {
                    _floofDb.Add(new Tag
                    {
                        TagName = tagName,
                        ServerId = Context.Guild.Id,
                        UserId = Context.User.Id,
                        TagContent = content
                    });
                    _floofDb.SaveChanges();
                    await SendEmbed(CreateDescriptionEmbed($"💾 Added Tag `{tag}`"));
                }
                catch (DbUpdateException e)
                {
                    await SendEmbed(CreateDescriptionEmbed($"💾 Tag `{tag}` Already Exists"));
                    Console.WriteLine(e);
                }
            }
            else
            {
                await SendEmbed(CreateDescriptionEmbed($"💾 Usage: `tag add [name] [content]`"));
            }
        }

        [Command("add")]
        [Priority(1)]
        public async Task Add()
        {
            await SendEmbed(CreateDescriptionEmbed($"💾 Usage: `tag add [name] [content]`"));
        }

        [Command("list")]
        [Summary("Lists all tags")]
        public async Task ListTags([Remainder] string content = null)
        {
            List<Tag> tags = _floofDb.Tags.AsQueryable()
                .Where(x => x.ServerId == Context.Guild.Id)
                .OrderBy(x => x.TagName)
                .ToList();
            List<string> pages = new List<string>();

            int index = 0;
            for (int i = 1; i <= (tags.Count / 50) + 1; i++)
            {
                string text = "```glsl\n";
                int pagebreak = index;
                for (; index < pagebreak + 50; index++)
                {
                    if (index < tags.Count)
                    {
                        text += $"[{index}] - {tags[index].TagName}\n";
                    }
                }

                text += "\n```";
                pages.Add(text);
            };

            // await PagedReplyAsync(pages);
        }

        [Command("remove")]
        [Summary("Removes a tag")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task Remove([Summary("Tag name")] string tag)
        {
            string tagName = $"{tag.ToLower()}:{Context.Guild.Id}";
            Tag tagToRemove = _floofDb.Tags.FirstOrDefault(x => x.TagName == tagName);
            if (tagToRemove != null)
            {
                try
                {
                    _floofDb.Remove(tagToRemove);
                    await _floofDb.SaveChangesAsync();
                    await SendEmbed(CreateDescriptionEmbed($"💾 Tag: `{tag}` Removed"));
                }
                catch (DbUpdateException)
                {
                    await SendEmbed(CreateDescriptionEmbed($"💾 Unable to remove Tag: `{tag}`"));
                }
            }
            else
            {
                await SendEmbed(CreateDescriptionEmbed($"💾 Could not find Tag: `{tag}`"));
            }
        }

        [Command("remove")]
        [Priority(1)]
        public async Task Remove()
        {
            await SendEmbed(CreateDescriptionEmbed($"💾 Usage: `tag remove [name]`"));
        }

        [Command]
        [Summary("Displays a tag")]
        [RequireUserPermission(GuildPermission.AttachFiles)]
        public async Task GetTag([Summary("Tag name")] string tag = "")
        {
            if (!string.IsNullOrEmpty(tag))
            {
                string tagName = $"{tag.ToLower()}:{Context.Guild.Id}";
                Tag selectedTag = _floofDb.Tags.AsQueryable().FirstOrDefault(x => x.TagName == tagName);

                if (selectedTag != null)
                {
                    string mentionlessTagContent = selectedTag.TagContent.Replace("@", "[at]");

                    bool isImage = false;
                    if (Uri.IsWellFormedUriString(mentionlessTagContent, UriKind.RelativeOrAbsolute))
                    {
                        string ext = mentionlessTagContent.Split('.').Last().ToLower();
                        isImage = SUPPORTED_IMAGE_EXTENSIONS.Contains(ext);
                    }

                    // tag found, so post it
                    if (isImage)
                    {
                        EmbedBuilder builder = new EmbedBuilder()
                        {
                            Title = "💾  " + tag.ToLower(),
                            Color = EMBED_COLOR
                        };
                        builder.WithImageUrl(mentionlessTagContent);
                        await SendEmbed(builder.Build());
                    }
                    else
                    {
                        await Context.Channel.SendMessageAsync(mentionlessTagContent);
                    }
                }
                else
                {
                    // tag not found
                    await SendEmbed(CreateDescriptionEmbed($"💾 Could not find Tag: `{tag}`"));
                }
            }
            else
            {
                // no tag given
                await SendEmbed(CreateDescriptionEmbed($"💾 Usage: `tag [name]`"));
            }
        }

        private Embed CreateDescriptionEmbed(string description)
        {
            EmbedBuilder builder = new EmbedBuilder
            {
                Description = description,
                Color = EMBED_COLOR
            };
            return builder.Build();
        }

        private Task SendEmbed(Embed embed)
        {
            return Context.Channel.SendMessageAsync("", false, embed);
        }
    }
}
