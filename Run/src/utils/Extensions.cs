using System;
using System.Collections.Generic;

namespace Run {
    public static class Extensions {
        public static void OnEach<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, Action<TSource> action) {
            foreach (TSource element in source) {
                if (predicate(element)) {
                    action(element);
                }
            }
        }

        public static void OnEach<TSource>(this IEnumerable<TSource> source, Action<TSource> action) {
            foreach (TSource element in source) {
                action(element);
            }
        }
        public static void OnEach<TSource, TCond>(this IEnumerable<TSource> source, Action<TCond> action) {
            foreach (TSource element in source) {
                if (element is TCond c) {
                    action(c);
                }
            }
        }
    }
}
