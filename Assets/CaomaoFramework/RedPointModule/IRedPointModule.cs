﻿using System;
using System.Collections.Generic;

namespace CaomaoFramework
{
    public interface IRedPointModule 
    {
        void BindUI(CUIRedPointButton button);
        void BindUI(params CUIRedPointButton[] buttons);
        void NotifyRedPoint(string id, bool bShow);
    }
}
