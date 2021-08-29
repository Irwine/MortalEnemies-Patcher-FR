// /*
//     Copyright (C) 2020  erri120
// 
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.
// */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Strings;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace MortalEnemies
{
    public static class Program
    {

        static Lazy<Settings> LazySettings = new Lazy<Settings>();
        static Settings Settings => LazySettings.Value;

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .SetAutogeneratedSettings(
                    "Settings",
                    "settings.json",
                    out LazySettings)
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "Mortal Enemies.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var config = Utils.FromJson<Config>(state.RetrieveConfigFile("config.json"));

            List<(IRaceGetter race, AttackData attackData)> races = state.LoadOrder.PriorityOrder
                .WinningOverrides<IRaceGetter>()
                .Where(x => x.EditorID != null)
                .Select(race =>
                {
                    try
                    {
                        List<KeyValuePair<string, List<string>>> classifications = config.Classifications
                            .Where(x => x.Key.Equals(race.EditorID!, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (classifications.Count == 1)
                            return (race, edid: classifications.First().Key);
                        
                        if (race.Name?.String == null) 
                            return (race, edid: string.Empty);
                        
                        classifications = config.Classifications.Where(x =>
                        {
                            var (_, value) = x;
                            return value.Any(y => y.Equals(race.Name.String, StringComparison.OrdinalIgnoreCase));
                        }).ToList();
                        
                        return (race, edid: classifications.First().Key);
                    }
                    catch (InvalidOperationException)
                    {
                        return (race, edid: string.Empty);
                    }
                })
                .SelectWhere(x =>
                {
                    if (!x.edid.IsNullOrWhitespace() 
                        && config.AttackData.TryGetValue(x.edid, out var attackData))
                    {
                        return TryGet<(IRaceGetter, AttackData)>.Succeed((x.race, attackData));
                    }
                    return TryGet<(IRaceGetter, AttackData)>.Failure;
                })
                .ToList();
            
            Utils.Log($"Found {races.Count} races to patch");
            foreach (var tuple in races)
            {
                var (race, attackData) = tuple;

                var patchedRace = state.PatchMod.Races.GetOrAddAsOverride(race);
                
                
                        string i18nRaceName = "";
                        if (patchedRace.Name != null 
                            && !patchedRace.Name.TryLookup(Language.French, out i18nRaceName)) {
                            i18nRaceName = patchedRace.Name.String;
                        }
                        patchedRace.Name = i18nRaceName;
                
                if (Math.Abs(attackData.AngleTolerance - float.MaxValue) > float.Epsilon)
                    patchedRace.AimAngleTolerance = attackData.AngleTolerance;
                if (Math.Abs(attackData.AngularAcceleration - float.MaxValue) > float.Epsilon)
                    patchedRace.AngularAccelerationRate = attackData.AngularAcceleration;
                if (Math.Abs(attackData.UnarmedReach - float.MaxValue) > float.Epsilon)
                    patchedRace.UnarmedReach = attackData.UnarmedReach;

                if (attackData.Attacks.Count == 0) continue;
                IEnumerable<(Mutagen.Bethesda.Skyrim.Attack attackToPatch, Attack data)> attacksToPatch = patchedRace.Attacks
                    .Where(x => x.AttackEvent != null)
                    .Where(x => x.AttackData != null)
                    .Where(x => attackData.Attacks.ContainsKey(x.AttackEvent!))
                    .Select(x =>
                    {
                        attackData.Attacks.TryGetValue(x.AttackEvent!, out var attack);
                        return (attackToPatch: x, data: attack!);
                    });

                foreach (var valueTuple in attacksToPatch)
                {
                    var (attackToPatch, data) = valueTuple;
                    
                    if (Math.Abs(data.StrikeAngle - float.MaxValue) > float.Epsilon)
                        attackToPatch.AttackData!.StrikeAngle = data.StrikeAngle;
                    if (Math.Abs(data.AttackAngle - float.MaxValue) > float.Epsilon)
                        attackToPatch.AttackData!.AttackAngle = data.AttackAngle;
                }
            }

            if (Settings.CommitmentMode != AttackCommitment.None)
            {
                MoveTypePatcher mtPatcher = new MoveTypePatcher(state, Settings);
                mtPatcher.run();
            }
        }
    }
}
