/*
Copyright (c) 2009-2010 Mikko Mononen memon@inside.org
recast4j copyright (c) 2015-2019 Piotr Piastucki piotr@jtilia.org
DotRecast Copyright (c) 2023 Choi Ikpil ikpil@naver.com

This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.
Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:
1. The origin of this software must not be misrepresented; you must not
 claim that you wrote the original software. If you use this software
 in a product, an acknowledgment in the product documentation would be
 appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
 misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
*/

using System.Collections.Immutable;
using System.Linq;

namespace DotRecast.Recast.DemoTool.Builder
{

    public class SampleAreaModifications
    {
        public const int SAMPLE_POLYAREA_TYPE_GROUND = 0x0;
        public const int SAMPLE_POLYAREA_TYPE_WATER = 0x1;
        public const int SAMPLE_POLYAREA_TYPE_ROAD = 0x2;
        public const int SAMPLE_POLYAREA_TYPE_DOOR = 0x3;
        public const int SAMPLE_POLYAREA_TYPE_GRASS = 0x4;
        public const int SAMPLE_POLYAREA_TYPE_JUMP = 0x5;
        public const int SAMPLE_POLYAREA_TYPE_JUMP_AUTO = 0x6;
        public const int SAMPLE_POLYAREA_TYPE_WALKABLE = 0x3f;

        public static readonly AreaModification SAMPLE_AREAMOD_WALKABLE = new AreaModification(SAMPLE_POLYAREA_TYPE_WALKABLE);
        public static readonly AreaModification SAMPLE_AREAMOD_GROUND = new AreaModification(SAMPLE_POLYAREA_TYPE_GROUND);
        public static readonly AreaModification SAMPLE_AREAMOD_WATER = new AreaModification(SAMPLE_POLYAREA_TYPE_WATER);
        public static readonly AreaModification SAMPLE_AREAMOD_ROAD = new AreaModification(SAMPLE_POLYAREA_TYPE_ROAD);
        public static readonly AreaModification SAMPLE_AREAMOD_GRASS = new AreaModification(SAMPLE_POLYAREA_TYPE_GRASS);
        public static readonly AreaModification SAMPLE_AREAMOD_DOOR = new AreaModification(SAMPLE_POLYAREA_TYPE_DOOR);
        public static readonly AreaModification SAMPLE_AREAMOD_JUMP = new AreaModification(SAMPLE_POLYAREA_TYPE_JUMP);

        public static readonly ImmutableArray<AreaModification> Values = ImmutableArray.Create(
            SAMPLE_AREAMOD_WALKABLE,
            SAMPLE_AREAMOD_GROUND,
            SAMPLE_AREAMOD_WATER,
            SAMPLE_AREAMOD_ROAD,
            SAMPLE_AREAMOD_GRASS,
            SAMPLE_AREAMOD_DOOR,
            SAMPLE_AREAMOD_JUMP
        );

        public static AreaModification OfValue(int value)
        {
            return Values.FirstOrDefault(x => x.Value == value) ?? SAMPLE_AREAMOD_GRASS;
        }

        public static readonly int SAMPLE_POLYFLAGS_WALK = 0x01; // Ability to walk (ground, grass, road)
        public static readonly int SAMPLE_POLYFLAGS_SWIM = 0x02; // Ability to swim (water).
        public static readonly int SAMPLE_POLYFLAGS_DOOR = 0x04; // Ability to move through doors.
        public static readonly int SAMPLE_POLYFLAGS_JUMP = 0x08; // Ability to jump.
        public static readonly int SAMPLE_POLYFLAGS_DISABLED = 0x10; // Disabled polygon
        public static readonly int SAMPLE_POLYFLAGS_ALL = 0xffff; // All abilities.
    }
}