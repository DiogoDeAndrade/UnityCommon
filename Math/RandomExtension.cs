﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

static class RandomExtension
{
    static public float Range(this System.Random rnd, float valMin, float valMax)
    {
        return (float)(rnd.NextDouble() * (valMax - valMin) + valMin);
    }

}