using System;

namespace EventFlow.Firebase.ReadStores.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FirebaseNodeNameAttribute : Attribute
    {
        private string nodeName;

        public FirebaseNodeNameAttribute(string nodeName)
        {
            this.nodeName = nodeName;
        }

        public virtual string NodeName
        {
            get { return nodeName; }
        }

    }
}