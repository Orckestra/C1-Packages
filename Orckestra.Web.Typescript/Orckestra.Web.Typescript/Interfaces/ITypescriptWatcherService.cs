﻿using System;
using System.Collections.Generic;

namespace Orckestra.Web.Typescript.Interfaces
{
    public interface ITypescriptWatcherService
    {
        bool InvokeService();
        bool ConfigureService(string customName, Action watchAction, string fileMask, IEnumerable<string> pathsToWatch);
    }
}