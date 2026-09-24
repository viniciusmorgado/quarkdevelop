//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See License.txt in the project root for license information.
//
// This file contain implementations details that are subject to change without notice.
// Use at your own risk.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.ComponentModel.Composition;
using System.ComponentModel;

namespace Microsoft.VisualStudio.Text.Operations.Standalone
{
    [Export(typeof(ITextUndoHistoryRegistry))]
    [Export(typeof(UndoHistoryRegistryImpl))]
    internal class UndoHistoryRegistryImpl : ITextUndoHistoryRegistry
    {
        #region Private Fields
        internal Dictionary<ITextUndoHistory, int> histories;
        private Dictionary<WeakReferenceForDictionaryKey, ITextUndoHistory> weakContextMapping;
        private Dictionary<object, ITextUndoHistory> strongContextMapping;
        #endregion // Private Fields

        public UndoHistoryRegistryImpl()
        {
            // set up the list of histories
            histories = new Dictionary<ITextUndoHistory, int>();

            // set up the mappings from contexts to histories
            weakContextMapping = new Dictionary<WeakReferenceForDictionaryKey, ITextUndoHistory>();
            strongContextMapping = new Dictionary<object, ITextUndoHistory>();
        }

        /// <summary>
        /// 
        /// </summary>
        public  IEnumerable<ITextUndoHistory> Histories
        {
            get { return histories.Keys; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public  ITextUndoHistory RegisterHistory(object context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return RegisterHistory(context, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="keepAlive"></param>
        /// <returns></returns>
        public  ITextUndoHistory RegisterHistory(object context, bool keepAlive)
        {
            ArgumentNullException.ThrowIfNull(context);

            ITextUndoHistory result;

            if (strongContextMapping.TryGetValue(context, out var value))
            {
                result = value;

                if (!keepAlive)
                {
                    strongContextMapping.Remove(context);
                    weakContextMapping.Add(new WeakReferenceForDictionaryKey(context), result);
                }
            }
            else if (weakContextMapping.ContainsKey(new WeakReferenceForDictionaryKey(context)))
            {
                result = weakContextMapping[new WeakReferenceForDictionaryKey(context)];

                if (keepAlive)
                {
                    weakContextMapping.Remove(new WeakReferenceForDictionaryKey(context));
                    strongContextMapping.Add(context, result);
                }
            }
            else
            {
                result = new UndoHistoryImpl(this);
                histories.Add(result, 1);

                if (keepAlive)
                {
                    strongContextMapping.Add(context, result);
                }
                else
                {
                    weakContextMapping.Add(new WeakReferenceForDictionaryKey(context), result);
                }
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public  ITextUndoHistory GetHistory(object context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ITextUndoHistory result;

            if (strongContextMapping.TryGetValue(context, out var value))
            {
                result = value;
            }
            else if (weakContextMapping.ContainsKey(new WeakReferenceForDictionaryKey(context)))
            {
                result = weakContextMapping[new WeakReferenceForDictionaryKey(context)];
            }
            else
            {
                throw new InvalidOperationException("Strings.GetHistoryCannotFindContextInRegistry");
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="history"></param>
        /// <returns></returns>
        public  bool TryGetHistory(object context, out ITextUndoHistory history)
        {
            ArgumentNullException.ThrowIfNull(context);

            ITextUndoHistory result = null;

            if (strongContextMapping.TryGetValue(context, out var value))
            {
                result = value;
            }
            else if (weakContextMapping.ContainsKey(new WeakReferenceForDictionaryKey(context)))
            {
                result = weakContextMapping[new WeakReferenceForDictionaryKey(context)];
            }

            history = result;
            return (result != null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="history"></param>
        public  void AttachHistory(object context, ITextUndoHistory history)
        {
            ArgumentNullException.ThrowIfNull(context);

            ArgumentNullException.ThrowIfNull(history);

            AttachHistory(context, history, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="history"></param>
        /// <param name="keepAlive"></param>
        public  void AttachHistory(object context, ITextUndoHistory history, bool keepAlive)
        {
            ArgumentNullException.ThrowIfNull(context);

            ArgumentNullException.ThrowIfNull(history);

            if (strongContextMapping.ContainsKey(context) || weakContextMapping.ContainsKey(new WeakReferenceForDictionaryKey(context)))
            {
                throw new InvalidOperationException("Strings.AttachHistoryAlreadyContainsContextInRegistry");
            }

            if (!histories.TryGetValue(history, out var value))
            {
                histories.Add(history, 1);
            }
            else
            {
                histories[history] = ++value;
            }

            if (keepAlive)
            {
                strongContextMapping.Add(context, history);
            }
            else
            {
                weakContextMapping.Add(new WeakReferenceForDictionaryKey(context), history);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="history"></param>
        public  void RemoveHistory(ITextUndoHistory history)
        {
            ArgumentNullException.ThrowIfNull(history);

            if (!histories.ContainsKey(history))
            {
                return;
            }

            histories.Remove(history);

            List<object> strongToRemove = new List<object>();
            foreach (object o in strongContextMapping.Keys)
            {
                if (Object.ReferenceEquals(strongContextMapping[o], history))
                {
                    strongToRemove.Add(o);
                }
            }
            strongToRemove.ForEach(delegate(object o) { strongContextMapping.Remove(o); });

            List<WeakReferenceForDictionaryKey> weakToRemove = new List<WeakReferenceForDictionaryKey>();
            foreach (WeakReferenceForDictionaryKey o in weakContextMapping.Keys)
            {
                if (Object.ReferenceEquals(weakContextMapping[o], history))
                {
                    weakToRemove.Add(o);
                }
            }
            weakToRemove.ForEach(delegate(WeakReferenceForDictionaryKey o) { weakContextMapping.Remove(o); });

            return;
        }
    }
}
