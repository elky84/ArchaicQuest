﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MIMWebClient.Core.Events;

namespace MIMWebClient.Core.Player.Skills
{
    using System.Runtime.CompilerServices;

    using MIMWebClient.Core.PlayerSetup;
    using MIMWebClient.Core.Room;

    public class DirtKick: Skill
    {

        public static Skill DirtKickSkill { get; set; }
        public static Skill DirtKickAb()
        {
                  
            if (DirtKickSkill != null)
            {
               return DirtKickSkill;
            }
            else
            {
                var skill = new Skill
                {
                    Name = "Dirt Kick",
                    CoolDown = 0,
                    Delay = 0,
                    LevelObtained = 7,
                    Proficiency = 1,
                    MaxProficiency = 95,
                    Passive = true,
                    UsableFromStatus = "Standing",
                    Syntax = "Passive command",
                    HelpText = new Help()
                    {
                        HelpText = "Dirt kick help text",
                        DateUpdated = new DateTime().ToShortDateString()
                    }
                };


                DirtKickSkill = skill;
            }

            return DirtKickSkill;
            
        }

        public   void StartDirtKick(IHubContext context, PlayerSetup.Player player, Room room, string target = "")
        {
            //Check if player has spell
            var hasSkill = Skill.CheckPlayerHasSkill(player, DirtKickAb().Name);

            if (hasSkill == false)
            {
                context.SendToClient("You don't know that skill.", player.HubGuid);
                return;
            }

            var canDoSkill = Skill.CanDoSkill(player);

            if (!canDoSkill)
            {
                return;
            }

            if (string.IsNullOrEmpty(target) && player.Target != null)
            {
                target = player.Target.Name;
            }

            var _target = Skill.FindTarget(target, room);

            //Fix issue if target has similar name to user and they use abbrivations to target them
            if (_target == player)
            {
                _target = null;
            }

            if (player.ActiveSkill != null)
            {

                context.SendToClient("wait till you have finished " + player.ActiveSkill.Name, player.HubGuid);
                return;

            }
            else
            {
                player.ActiveSkill = DirtKickAb();
            }




            if (_target != null)
            {
 

                if (_target.HitPoints <= 0)
                {
                    context.SendToClient("You can't dirt kick them as they are dead.", player.HubGuid);
                    return;

                }

                if (player.MovePoints < DirtKickAb().MovesCost)
                {


                    context.SendToClient("You are too tired to use dirt kick.", player.HubGuid);
                    player.ActiveSkill = null;

                    return;
                }

                player.MovePoints -= DirtKickAb().MovesCost;

                Score.UpdateUiPrompt(player);



                var chanceOfSuccess = Helpers.Rand(1, 100);
                var skill = player.Skills.FirstOrDefault(x => x.Name.Equals("Dirt Kick"));
                if (skill == null)
                {
                    player.ActiveSkill = null;
                    return;
                }

                var skillProf = skill.Proficiency;

                if (skillProf >= chanceOfSuccess)
                {
                     Task.Run(() => DoSkill(context, player, _target, room));
                }
                else
                {
                    player.ActiveSkill = null;
                    HubContext.Instance.SendToClient("You don't see any dirt here to kick.",
                        player.HubGuid);
                    PlayerSetup.Player.LearnFromMistake(player, DirtKickAb(), 250);
         

                    Score.ReturnScoreUI(player);
                }
            }
            else if (_target == null)
            {
                context.SendToClient($"You can't find anything known as '{target}' here.", player.HubGuid);
                player.ActiveSkill = null;
            }


        }

        private int DirtKickSuccess(PlayerSetup.Player attacker, PlayerSetup.Player target)
        {
            var success = 2 * attacker.Dexterity - target.Dexterity + (attacker.Level - target.Level) * 2;

            if (attacker.Effects.FirstOrDefault(x => x.Name.Equals("Haste")) != null)
            {
                success += 10;
            }

            if (target.Effects.FirstOrDefault(x => x.Name.Equals("Haste")) != null)
            {
                success -= 25;
            }

            if (success <= 0)
            {
                success = 1;
            }

            return success;
        }

        private async Task DoSkill(IHubContext context, PlayerSetup.Player attacker, PlayerSetup.Player target, Room room)
        {

            attacker.Status = PlayerSetup.Player.PlayerStatus.Busy;

            await Task.Delay(500);

            if (attacker.ManaPoints < DirtKickAb().MovesCost)
            {
                context.SendToClient("You are too tired to use dirt kick.", attacker.HubGuid);
                attacker.ActiveSkill = null;
                PlayerSetup.Player.SetState(attacker);
                return;
            }
           
            if (Effect.HasEffect(target, Effect.Blindness(attacker).Name))
            {
                context.SendToClient("They are already blinded.", attacker.HubGuid);
                attacker.ActiveSkill = null;
                PlayerSetup.Player.SetState(attacker);
                return;
            }


            var die = new PlayerStats();
  
            bool alive = Fight2.IsAlive(attacker, target);

            if (alive)
            {
         
                    var skillSuccess = DirtKickSuccess(attacker, target);
                    int chanceOfDirtKick = die.dice(1, 100);

                    if (skillSuccess > chanceOfDirtKick)
                    {
                        HubContext.Instance.SendToClient(
                            "<span style='color:cyan'>You kick dirt into the eyes of " +
                            Helpers.ReturnName(target, attacker, null).ToLower() + ".</span>",
                            attacker.HubGuid);

                        HubContext.Instance.SendToClient(
                            $"Your dirt kick blinds {Helpers.ReturnName(target, attacker, null).ToLower()}.",
                            attacker.HubGuid);


                        HubContext.Instance.SendToClient(
                            $"<span style='color:cyan'>{Helpers.ReturnName(attacker, target, null)} kicks dirt in your eyes!</span>",
                            target.HubGuid);

                        HubContext.Instance.SendToClient(
                            $"<span style='color:cyan'>You can't see a thing!</span>",
                            target.HubGuid);

                        var blindEffect = Effect.Blindness(attacker);

                        if (!Effect.HasEffect(target, blindEffect.Name))
                        {
                            target.Effects.Add(blindEffect);

                        }

                        foreach (var player in room.players)
                        {
                            if (player != attacker && player != target)
                            {

                                HubContext.Instance.SendToClient(
                                    Helpers.ReturnName(attacker, target, null) + " kicks dirt into the eyes of " +
                                    Helpers.ReturnName(target, attacker, null) + ".", target.HubGuid);

                            }


                        }

                    }
                    else
                    {
                        var attackerMessage = "Your dirt kick <span style='color:olive'>misses</span> " +
                                                 Helpers.ReturnName(target, attacker, null);

                        var targetMessage = Helpers.ReturnName(attacker, target, null) +
                                               "'s dirt kick <span style='color:olive'>misses</span> you ";

                        var observerMessage = Helpers.ReturnName(attacker, target, null) +
                                                 "'s dirt kick <span style='color:olive'>misses</span> " +
                                                 Helpers.ReturnName(target, attacker, null);


                        HubContext.Instance.SendToClient(attackerMessage, attacker.HubGuid);
                        HubContext.Instance.SendToClient(targetMessage, target.HubGuid);

                        foreach (var player in room.players)
                        {
                            if (player != attacker && player != target)
                            {
                                HubContext.Instance.SendToClient(
                                    observerMessage, player.HubGuid);
                            }
                        }


                    }
 

            }

 
            Score.ReturnScoreUI(target);

            PlayerSetup.Player.SetState(attacker);

            Fight2.PerpareToFightBack(attacker, room, target.Name, true);


            attacker.ActiveSkill = null;

        }
    }
}
