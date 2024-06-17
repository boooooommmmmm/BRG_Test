﻿using System;

namespace BRGContainer.Runtime
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class BRGMethodThreadSafeAttribute : Attribute
    {
        
    }
    
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class BRGClassThreadSafeAttribute : Attribute
    {
        
    }
    
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    public class BRGValueThreadSafeAttribute : Attribute
    {
        
    }
    
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    public class BRGValueThreadUnsafeAttribute : Attribute
    {
        
    }
}