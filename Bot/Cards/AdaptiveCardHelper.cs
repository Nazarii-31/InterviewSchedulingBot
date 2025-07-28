using AdaptiveCards;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using InterviewBot.Domain.Entities;

namespace InterviewBot.Bot.Cards
{
    public static class AdaptiveCardHelper
    {
        public static Attachment CreateTimeSlotSelectionCard(List<RankedTimeSlot> slots, string interviewTitle)
        {
            var card = new AdaptiveCard("1.6")
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock($"🎯 **Available Time Slots for '{interviewTitle}'**")
                    {
                        Size = AdaptiveTextSize.Large,
                        Weight = AdaptiveTextWeight.Bolder,
                        Color = AdaptiveTextColor.Accent
                    },
                    new AdaptiveTextBlock("Please select your preferred interview time:")
                    {
                        Size = AdaptiveTextSize.Medium,
                        Spacing = AdaptiveSpacing.Medium
                    }
                }
            };

            // Add each time slot as a container with action
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var container = CreateSlotContainer(slot, i);
                card.Body.Add(container);
            }

            // Add actions
            card.Actions = new List<AdaptiveAction>
            {
                new AdaptiveSubmitAction
                {
                    Title = "Cancel",
                    Data = new { action = "cancel" }
                }
            };

            return new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                Content = card
            };
        }

        private static AdaptiveContainer CreateSlotContainer(RankedTimeSlot slot, int index)
        {
            var timeStr = slot.StartTime.ToString("ddd, MMM dd 'at' h:mm tt");
            var durationStr = $"{slot.Duration.TotalMinutes} min";
            var availabilityStr = $"{slot.AvailableParticipants}/{slot.TotalParticipants} participants";
            var scoreIcon = GetScoreIcon(slot.Score);

            return new AdaptiveContainer
            {
                Style = AdaptiveContainerStyle.Emphasis,
                Spacing = AdaptiveSpacing.Medium,
                Items = new List<AdaptiveElement>
                {
                    new AdaptiveColumnSet
                    {
                        Columns = new List<AdaptiveColumn>
                        {
                            new AdaptiveColumn
                            {
                                Width = "stretch",
                                Items = new List<AdaptiveElement>
                                {
                                    new AdaptiveTextBlock($"🗓️ **{timeStr}**")
                                    {
                                        Weight = AdaptiveTextWeight.Bolder,
                                        Size = AdaptiveTextSize.Medium
                                    },
                                    new AdaptiveTextBlock($"⏱️ {durationStr} | 👥 {availabilityStr}")
                                    {
                                        Size = AdaptiveTextSize.Small,
                                        Color = AdaptiveTextColor.Good
                                    }
                                }
                            },
                            new AdaptiveColumn
                            {
                                Width = "auto",
                                Items = new List<AdaptiveElement>
                                {
                                    new AdaptiveTextBlock($"{scoreIcon} {slot.Score:F1}")
                                    {
                                        Size = AdaptiveTextSize.Small,
                                        Weight = AdaptiveTextWeight.Bolder,
                                        Color = GetScoreColor(slot.Score)
                                    }
                                }
                            }
                        }
                    }
                },
                SelectAction = new AdaptiveSubmitAction
                {
                    Data = new { action = "selectSlot", slotIndex = index }
                }
            };
        }

        public static Attachment CreateInterviewConfirmationCard(
            string title, 
            DateTime startTime, 
            int durationMinutes, 
            List<string> participants,
            int availableCount,
            int totalCount)
        {
            var card = new AdaptiveCard("1.6")
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock("📅 **Interview Confirmation**")
                    {
                        Size = AdaptiveTextSize.Large,
                        Weight = AdaptiveTextWeight.Bolder,
                        Color = AdaptiveTextColor.Accent
                    },
                    new AdaptiveFactSet
                    {
                        Facts = new List<AdaptiveFact>
                        {
                            new AdaptiveFact("📋 Title", title),
                            new AdaptiveFact("🗓️ Date", startTime.ToString("ddd, MMM dd, yyyy")),
                            new AdaptiveFact("⏰ Time", startTime.ToString("h:mm tt")),
                            new AdaptiveFact("⏱️ Duration", $"{durationMinutes} minutes"),
                            new AdaptiveFact("👥 Availability", $"{availableCount}/{totalCount} participants"),
                            new AdaptiveFact("📍 Location", "Microsoft Teams Meeting")
                        }
                    },
                    new AdaptiveTextBlock("**Participants:**")
                    {
                        Weight = AdaptiveTextWeight.Bolder,
                        Spacing = AdaptiveSpacing.Medium
                    },
                    new AdaptiveTextBlock(string.Join("\n", participants.Select(p => $"• {p}")))
                    {
                        Size = AdaptiveTextSize.Small
                    }
                },
                Actions = new List<AdaptiveAction>
                {
                    new AdaptiveSubmitAction
                    {
                        Title = "✅ Confirm & Schedule",
                        Data = new { action = "confirm" }
                    },
                    new AdaptiveSubmitAction
                    {
                        Title = "❌ Cancel",
                        Data = new { action = "cancel" }
                    },
                    new AdaptiveSubmitAction
                    {
                        Title = "🔄 Choose Different Time",
                        Data = new { action = "back" }
                    }
                }
            };

            return new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                Content = card
            };
        }

        public static Attachment CreateSchedulingSuccessCard(
            string title,
            DateTime startTime,
            int durationMinutes,
            List<string> participants)
        {
            var card = new AdaptiveCard("1.6")
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock("🎉 **Interview Scheduled Successfully!**")
                    {
                        Size = AdaptiveTextSize.Large,
                        Weight = AdaptiveTextWeight.Bolder,
                        Color = AdaptiveTextColor.Good
                    },
                    new AdaptiveTextBlock("Your interview has been scheduled and all participants will receive calendar invitations.")
                    {
                        Wrap = true,
                        Spacing = AdaptiveSpacing.Medium
                    },
                    new AdaptiveContainer
                    {
                        Style = AdaptiveContainerStyle.Emphasis,
                        Items = new List<AdaptiveElement>
                        {
                            new AdaptiveFactSet
                            {
                                Facts = new List<AdaptiveFact>
                                {
                                    new AdaptiveFact("📋 Title", title),
                                    new AdaptiveFact("🗓️ Date & Time", startTime.ToString("ddd, MMM dd, yyyy 'at' h:mm tt")),
                                    new AdaptiveFact("⏱️ Duration", $"{durationMinutes} minutes"),
                                    new AdaptiveFact("👥 Participants", $"{participants.Count} people"),
                                    new AdaptiveFact("📍 Meeting", "Teams meeting link will be sent")
                                }
                            }
                        }
                    }
                },
                Actions = new List<AdaptiveAction>
                {
                    new AdaptiveSubmitAction
                    {
                        Title = "📅 Schedule Another Interview",
                        Data = new { action = "scheduleAnother" }
                    },
                    new AdaptiveSubmitAction
                    {
                        Title = "📋 View My Interviews",
                        Data = new { action = "viewInterviews" }
                    }
                }
            };

            return new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                Content = card
            };
        }

        private static string GetScoreIcon(double score)
        {
            return score switch
            {
                >= 90 => "🟢",
                >= 70 => "🟡",
                >= 50 => "🟠",
                _ => "🔴"
            };
        }

        private static AdaptiveTextColor GetScoreColor(double score)
        {
            return score switch
            {
                >= 80 => AdaptiveTextColor.Good,
                >= 60 => AdaptiveTextColor.Warning,
                _ => AdaptiveTextColor.Attention
            };
        }
    }
}