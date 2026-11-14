using System; using System.Diagnostics; using System.Threading; using System.Windows.Automation; namespace T3Usage {public sealed class T3Driver {
    [ThreadStatic]static Stopwatch operationClock;static void CheckDeadline(){if(operationClock!=null&&operationClock.ElapsedMilliseconds>=2500)throw new TimeoutException("UIA operation expired");}
    readonly T3Window tracker;
    readonly System.Threading.SemaphoreSlim serial = new System.Threading.SemaphoreSlim(1,1);
    public T3Driver(T3Window window){tracker=window;}
    public async System.Threading.Tasks.Task<string> RunAsync(string op,string provider,string model,string optLabel,string optValue){
        await serial.WaitAsync().ConfigureAwait(false);
        try {tracker.Paused=true;return await UiaDeadline.Run(()=>{operationClock=Stopwatch.StartNew();return Exec(op,provider,model,optLabel,optValue);},2500).ConfigureAwait(false);}
        catch(Exception e){Log.Error("Driver",e);return "error:"+e.Message;}
        finally{tracker.Paused=false;serial.Release();}
    }
    AutomationElement Root(){return tracker.Current.Hwnd==IntPtr.Zero?null:AutomationElement.FromHandle(tracker.Current.Hwnd);}
    static AutomationElement Wait(Func<AutomationElement> f, int ms) {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) {CheckDeadline();
            try { var e = f();CheckDeadline();if (e != null) return e; } catch (TimeoutException){throw;}catch { }
            Thread.Sleep(15);
        }
        return null;
    }

    static AutomationElement FindIn(AutomationElement scope, ControlType ct, Func<string, bool> nameOk) {CheckDeadline();
        var cr = new CacheRequest();
        cr.Add(AutomationElement.NameProperty);
        AutomationElementCollection all;
        using (cr.Activate()) {
            all = scope.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ct));
        }
        foreach (AutomationElement e in all) {
            CheckDeadline();string n = (e.GetCachedPropertyValue(AutomationElement.NameProperty) as string) ?? "";
            if (nameOk(n)) return e;
        }
        return null;
    }

    static bool LabelMatch(string name, string label) {
        return name == label || name.StartsWith(label + " ", StringComparison.Ordinal) || name.StartsWith(label + ":", StringComparison.Ordinal);
    }

    static ExpandCollapsePattern Picker(AutomationElement root) {
        System.Windows.Rect r; string n;
        var trig = T3Window.FindTrigger(root, out r, out n);
        if (trig == null) return null;
        try { return trig.GetCurrentPattern(ExpandCollapsePattern.Pattern) as ExpandCollapsePattern; }
        catch (InvalidOperationException) { return null; }
        catch (ElementNotAvailableException) { return null; }
    }

    static AutomationElement OpenPicker(AutomationElement root, out ExpandCollapsePattern ec) {
        ec = null;
        AutomationElement bar = null;
        for (int attempt = 0; attempt < 3 && bar == null; attempt++) {
            CheckDeadline();
            try {
                ec = Picker(root);
                if (ec == null) { Thread.Sleep(120); continue; }
                if (ec.Current.ExpandCollapseState != ExpandCollapseState.Expanded) ec.Expand();
            } catch (InvalidOperationException) { Thread.Sleep(80); continue; }
            catch (ElementNotAvailableException) { Thread.Sleep(80); continue; }
            bar = Wait(() => root.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar),
                new PropertyCondition(AutomationElement.NameProperty, "Providers"))), 1200);
            if (bar == null) { try { ec.Collapse(); } catch { } Thread.Sleep(120); }
        }
        if (bar == null) return null;
        // The popup window that contains the provider rail.
        var walker = TreeWalker.ControlViewWalker;
        var p = bar;
        while (p != null && p.Current.ControlType != ControlType.Window) p = walker.GetParent(p);
        return p ?? bar;
    }

    static void ClosePicker(AutomationElement root) {
        try {
            var ec = Picker(root);
            if (ec != null && ec.Current.ExpandCollapseState == ExpandCollapseState.Expanded) {
                CheckDeadline();ec.Collapse();
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 220) {
                    if (ec.Current.ExpandCollapseState != ExpandCollapseState.Expanded) break;
                    Thread.Sleep(15);
                }
            }
        } catch { }
    }

    // op: select | favorite | option | native
    string Exec(string op, string provider, string model, string optLabel, string optValue) {
        var root = Root();
        if (root == null) return "error:T3 Code not found";
        ExpandCollapsePattern ec = Picker(root);
        if (ec == null) {
            // Accessibility trees warm up lazily; touch it once only if trigger wasn't ready.
            root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            ec = Picker(root);
        }
        if (op == "native") {
            if (ec == null) return "error:model picker not found";
            CheckDeadline();ec.Expand();
            return "ok";
        }
        bool keepOpen = false;
        try {
            var popup = OpenPicker(root, out ec);
            if (popup == null) return "error:T3 model picker did not open";

            var rail = FindIn(popup, ControlType.Button, n => LabelMatch(n, provider));
            if (rail == null) return "error:" + provider + " is not in the T3 model picker";
            if (rail.Current.Name.StartsWith(provider + ":", StringComparison.Ordinal)) return "unavailable:" + rail.Current.Name;
            object tp;
            if (rail.TryGetCurrentPattern(TogglePattern.Pattern, out tp) && ((TogglePattern)tp).Current.ToggleState != ToggleState.On) {
                CheckDeadline();((TogglePattern)tp).Toggle();
                Thread.Sleep(35);
            }

            string rowPrefix = model + " , " + provider;
            Func<AutomationElement> findRow = () => FindIn(popup, ControlType.ListItem, n => n.StartsWith(rowPrefix, StringComparison.Ordinal));
            var row = Wait(findRow, 650);
            if (row == null) {
                // Long lists are virtualized: filter with T3's own search field.
                var search = FindIn(popup, ControlType.ComboBox, n => true);
                object vp;
                if (search != null && search.TryGetCurrentPattern(ValuePattern.Pattern, out vp)) {
                    CheckDeadline();((ValuePattern)vp).SetValue(model);
                    row = Wait(findRow, 1200);
                }
            }
            if (row == null) return "error:model " + model + " not found in the T3 picker";
            if (!row.Current.IsEnabled) return "unavailable:" + model + " cannot be selected in this thread";

            if (op == "favorite") {
                var fav = FindIn(row, ControlType.Button, n => n.Contains("favorites"));
                if (fav == null) return "error:favorite button not found";
                var favoriteInvoke=(InvokePattern)fav.GetCurrentPattern(InvokePattern.Pattern);CheckDeadline();favoriteInvoke.Invoke();
                Thread.Sleep(60);
                return "ok";
            }

            var rowInvoke=(InvokePattern)row.GetCurrentPattern(InvokePattern.Pattern);CheckDeadline();rowInvoke.Invoke();
            if (op == "select" && string.IsNullOrEmpty(optValue)) {
                Thread.Sleep(60);
                return "ok";
            }
            Thread.Sleep(100);

            // Options apply to the selected model; the picker reopens on it.
            popup = OpenPicker(root, out ec);
            if (popup == null) return "error:T3 model picker did not reopen";
            if (op == "select") {
                // Reasoning-style option: a radio button.
                var radio = Wait(() => FindIn(popup, ControlType.RadioButton, n => LabelMatch(n, optValue)), 1200);
                if (radio == null) return "error:option " + optValue + " not found";
                var radioInvoke=(InvokePattern)radio.GetCurrentPattern(InvokePattern.Pattern);CheckDeadline();radioInvoke.Invoke();
                Thread.Sleep(60);
                return "ok";
            }
            if (op == "option") {
                var btn = Wait(() => FindIn(popup, ControlType.Button, n => n.StartsWith(optLabel + ":", StringComparison.Ordinal)), 1200);
                if (btn == null) return "error:" + optLabel + " not found";
                var optionExpand=(ExpandCollapsePattern)btn.GetCurrentPattern(ExpandCollapsePattern.Pattern);CheckDeadline();optionExpand.Expand();
                Func<AutomationElement> findItem = () => {
                    foreach (var ct in new[] { ControlType.MenuItem, ControlType.RadioButton, ControlType.ListItem }) {
                        var e = FindIn(root, ct, n => LabelMatch(n, optValue));
                        if (e != null) return e;
                    }
                    return null;
                };
                var item = Wait(findItem, 1200);
                if (item == null) return "error:choice " + optValue + " not found";
                object ip;
                if (item.TryGetCurrentPattern(InvokePattern.Pattern, out ip)){CheckDeadline();((InvokePattern)ip).Invoke();}
                else if (item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out ip)){CheckDeadline();((SelectionItemPattern)ip).Select();}
                Thread.Sleep(60);
                return "ok";
            }
            return "error:unknown operation";
        } finally {
            if (!keepOpen && operationClock.ElapsedMilliseconds<2500) ClosePicker(root);
        }
    }
}

}
