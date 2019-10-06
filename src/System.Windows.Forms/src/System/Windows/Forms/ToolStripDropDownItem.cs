﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms.Layout;
using static Interop;

namespace System.Windows.Forms
{
    /// <summary>
    ///  Base class for ToolStripItems that display DropDown windows.
    /// </summary>
    [Designer("System.Windows.Forms.Design.ToolStripMenuItemDesigner, " + AssemblyRef.SystemDesign)]
    [DefaultProperty(nameof(DropDownItems))]
    public abstract class ToolStripDropDownItem : ToolStripItem
    {
        private ToolStripDropDown dropDown = null;
        private ToolStripDropDownDirection toolStripDropDownDirection = ToolStripDropDownDirection.Default;
        private static readonly object EventDropDownShow = new object();
        private static readonly object EventDropDownHide = new object();
        private static readonly object EventDropDownOpened = new object();
        private static readonly object EventDropDownClosed = new object();
        private static readonly object EventDropDownItemClicked = new object();

        /// <summary>
        ///  Protected ctor so you can't create one of these without deriving from it.
        /// </summary>
        protected ToolStripDropDownItem()
        {
        }

        protected ToolStripDropDownItem(string text, Image image, EventHandler onClick) : base(text, image, onClick)
        {
        }

        protected ToolStripDropDownItem(string text, Image image, EventHandler onClick, string name) : base(text, image, onClick, name)
        {
        }

        protected ToolStripDropDownItem(string text, Image image, params ToolStripItem[] dropDownItems) : this(text, image, (EventHandler)null)
        {
            if (dropDownItems != null)
            {
                DropDownItems.AddRange(dropDownItems);
            }
        }

        /// <summary>
        ///  The ToolStripDropDown that will be displayed when this item is clicked.
        /// </summary>
        [
        TypeConverter(typeof(ReferenceConverter)),
        SRCategory(nameof(SR.CatData)),
        SRDescription(nameof(SR.ToolStripDropDownDescr))
        ]
        public ToolStripDropDown DropDown
        {
            get
            {
                if (dropDown == null)
                {
                    DropDown = CreateDefaultDropDown();
                    if (!(this is ToolStripOverflowButton))
                    {
                        dropDown.SetAutoGeneratedInternal(true);
                    }

                    if (ParentInternal != null)
                    {
                        dropDown.ShowItemToolTips = ParentInternal.ShowItemToolTips;
                    }
                }
                return dropDown;
            }
            set
            {
                if (dropDown != value)
                {

                    if (dropDown != null)
                    {
                        dropDown.Opened -= new EventHandler(DropDown_Opened);
                        dropDown.Closed -= new ToolStripDropDownClosedEventHandler(DropDown_Closed);
                        dropDown.ItemClicked -= new ToolStripItemClickedEventHandler(DropDown_ItemClicked);
                        dropDown.UnassignDropDownItem();
                    }

                    dropDown = value;
                    if (dropDown != null)
                    {
                        dropDown.Opened += new EventHandler(DropDown_Opened);
                        dropDown.Closed += new ToolStripDropDownClosedEventHandler(DropDown_Closed);
                        dropDown.ItemClicked += new ToolStripItemClickedEventHandler(DropDown_ItemClicked);
                        dropDown.AssignToDropDownItem();
                    }

                }

            }
        }

        // the area which activates the dropdown.
        internal virtual Rectangle DropDownButtonArea
            => Bounds;

        [Browsable(false)]
        [SRDescription(nameof(SR.ToolStripDropDownItemDropDownDirectionDescr))]
        [SRCategory(nameof(SR.CatBehavior))]
        public ToolStripDropDownDirection DropDownDirection
        {
            get
            {
                if (toolStripDropDownDirection == ToolStripDropDownDirection.Default)
                {
                    ToolStrip parent = ParentInternal;
                    if (parent != null)
                    {
                        ToolStripDropDownDirection dropDownDirection = parent.DefaultDropDownDirection;
                        if (OppositeDropDownAlign || RightToLeft != parent.RightToLeft && (RightToLeft != RightToLeft.Inherit))
                        {
                            dropDownDirection = RTLTranslateDropDownDirection(dropDownDirection, RightToLeft);
                        }

                        if (IsOnDropDown)
                        {
                            // we gotta make sure that we dont collide with the existing menu.
                            Rectangle bounds = GetDropDownBounds(dropDownDirection);
                            Rectangle ownerItemBounds = new Rectangle(TranslatePoint(Point.Empty, ToolStripPointType.ToolStripItemCoords, ToolStripPointType.ScreenCoords), Size);
                            Rectangle intersectionBetweenChildAndParent = Rectangle.Intersect(bounds, ownerItemBounds);

                            // grab the intersection
                            if (intersectionBetweenChildAndParent.Width >= 2)
                            {
                                RightToLeft toggledRightToLeft = (RightToLeft == RightToLeft.Yes) ? RightToLeft.No : RightToLeft.Yes;
                                ToolStripDropDownDirection newDropDownDirection = RTLTranslateDropDownDirection(dropDownDirection, toggledRightToLeft);

                                // verify that changing the dropdown direction actually causes less intersection.
                                int newIntersectionWidth = Rectangle.Intersect(GetDropDownBounds(newDropDownDirection), ownerItemBounds).Width;
                                if (newIntersectionWidth < intersectionBetweenChildAndParent.Width)
                                {
                                    dropDownDirection = newDropDownDirection;
                                }
                            }

                        }
                        return dropDownDirection;

                    }
                }

                // someone has set a custom override
                return toolStripDropDownDirection;

            }
            set
            {
                // cant use Enum.IsValid as its not sequential
                switch (value)
                {
                    case ToolStripDropDownDirection.AboveLeft:
                    case ToolStripDropDownDirection.AboveRight:
                    case ToolStripDropDownDirection.BelowLeft:
                    case ToolStripDropDownDirection.BelowRight:
                    case ToolStripDropDownDirection.Left:
                    case ToolStripDropDownDirection.Right:
                    case ToolStripDropDownDirection.Default:
                        break;
                    default:
                        throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ToolStripDropDownDirection));
                }

                if (toolStripDropDownDirection != value)
                {
                    toolStripDropDownDirection = value;
                    if (HasDropDownItems && DropDown.Visible)
                    {
                        DropDown.Location = DropDownLocation;
                    }
                }
            }
        }

        /// <summary>
        ///  Occurs when the dropdown is closed
        /// </summary>
        [
        SRCategory(nameof(SR.CatAction)),
        SRDescription(nameof(SR.ToolStripDropDownClosedDecr))
        ]
        public event EventHandler DropDownClosed
        {
            add => Events.AddHandler(EventDropDownClosed, value);
            remove => Events.RemoveHandler(EventDropDownClosed, value);
        }

        internal protected virtual Point DropDownLocation
        {
            get
            {

                if (ParentInternal == null || !HasDropDownItems)
                {
                    return Point.Empty;
                }
                ToolStripDropDownDirection dropDownDirection = DropDownDirection;
                return GetDropDownBounds(dropDownDirection).Location;
            }
        }

        [
        SRCategory(nameof(SR.CatAction)),
        SRDescription(nameof(SR.ToolStripDropDownOpeningDescr))
        ]
        public event EventHandler DropDownOpening
        {
            add => Events.AddHandler(EventDropDownShow, value);
            remove => Events.RemoveHandler(EventDropDownShow, value);
        }
        /// <summary>
        ///  Occurs when the dropdown is opened
        /// </summary>
        [
        SRCategory(nameof(SR.CatAction)),
        SRDescription(nameof(SR.ToolStripDropDownOpenedDescr))
        ]
        public event EventHandler DropDownOpened
        {
            add => Events.AddHandler(EventDropDownOpened, value);
            remove => Events.RemoveHandler(EventDropDownOpened, value);
        }

        /// <summary>
        ///  Returns the DropDown's items collection.
        /// </summary>
        [
        DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
        SRCategory(nameof(SR.CatData)),
        SRDescription(nameof(SR.ToolStripDropDownItemsDescr))
        ]
        public ToolStripItemCollection DropDownItems
            => DropDown.Items;

        /// <summary>
        ///  Occurs when the dropdown is opened
        /// </summary>
        [SRCategory(nameof(SR.CatAction))]
        public event ToolStripItemClickedEventHandler DropDownItemClicked
        {
            add => Events.AddHandler(EventDropDownItemClicked, value);
            remove => Events.RemoveHandler(EventDropDownItemClicked, value);
        }

        [Browsable(false)]
        public virtual bool HasDropDownItems
            =>
                //Use count of visible DisplayedItems instead so that we take into account things that arent visible
                (dropDown != null) && dropDown.HasVisibleItems;

        [Browsable(false)]
        public bool HasDropDown
            => dropDown != null;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override bool Pressed
        {
            get
            {
                if (dropDown != null)
                {
                    if (DropDown.AutoClose || !IsInDesignMode || (IsInDesignMode && !IsOnDropDown))
                    {
                        return DropDown.OwnerItem == this && DropDown.Visible;
                    }
                }
                return base.Pressed;
            }
        }

        internal virtual bool OppositeDropDownAlign
            => false;

        internal virtual void AutoHide(ToolStripItem otherItemBeingSelected)
            => HideDropDown();

        protected override AccessibleObject CreateAccessibilityInstance()
            => new ToolStripDropDownItemAccessibleObject(this);

        protected virtual ToolStripDropDown CreateDefaultDropDown()
        {
            // AutoGenerate a ToolStrip DropDown - set the property so we hook events
            return new ToolStripDropDown(this, true);
        }

        private Rectangle DropDownDirectionToDropDownBounds(ToolStripDropDownDirection dropDownDirection, Rectangle dropDownBounds)
        {
            Point offset = Point.Empty;

            switch (dropDownDirection)
            {
                case ToolStripDropDownDirection.AboveLeft:
                    offset.X = -dropDownBounds.Width + Width;
                    offset.Y = -dropDownBounds.Height + 1;
                    break;
                case ToolStripDropDownDirection.AboveRight:
                    offset.Y = -dropDownBounds.Height + 1;
                    break;
                case ToolStripDropDownDirection.BelowRight:
                    offset.Y = Height - 1;
                    break;
                case ToolStripDropDownDirection.BelowLeft:
                    offset.X = -dropDownBounds.Width + Width;
                    offset.Y = Height - 1;
                    break;
                case ToolStripDropDownDirection.Right:
                    offset.X = Width;
                    if (!IsOnDropDown)
                    {
                        // overlap the toplevel toolstrip
                        offset.X--;
                    }
                    break;

                case ToolStripDropDownDirection.Left:
                    offset.X = -dropDownBounds.Width;
                    break;
            }

            Point itemScreenLocation = TranslatePoint(Point.Empty, ToolStripPointType.ToolStripItemCoords, ToolStripPointType.ScreenCoords);
            dropDownBounds.Location = new Point(itemScreenLocation.X + offset.X, itemScreenLocation.Y + offset.Y);
            dropDownBounds = WindowsFormsUtils.ConstrainToScreenWorkingAreaBounds(dropDownBounds);
            return dropDownBounds;
        }

        private void DropDown_Closed(object sender, ToolStripDropDownClosedEventArgs e)
            => OnDropDownClosed(EventArgs.Empty);

        private void DropDown_Opened(object sender, EventArgs e)
            => OnDropDownOpened(EventArgs.Empty);

        private void DropDown_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
            => OnDropDownItemClicked(e);

        /// <summary>
        ///  Make sure we unhook dropdown events.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (dropDown != null)
            {
                dropDown.Opened -= new EventHandler(DropDown_Opened);
                dropDown.Closed -= new ToolStripDropDownClosedEventHandler(DropDown_Closed);
                dropDown.ItemClicked -= new ToolStripItemClickedEventHandler(DropDown_ItemClicked);

                if (disposing && dropDown.IsAutoGenerated)
                {
                    // if we created the dropdown, dispose it and its children.
                    dropDown.Dispose();
                    dropDown = null;
                }
            }
            base.Dispose(disposing);
        }

        private Rectangle GetDropDownBounds(ToolStripDropDownDirection dropDownDirection)
        {
            Rectangle dropDownBounds = new Rectangle(Point.Empty, DropDown.GetSuggestedSize());
            // calculate the offset from the upper left hand corner of the item.
            dropDownBounds = DropDownDirectionToDropDownBounds(dropDownDirection, dropDownBounds);

            // we should make sure we dont obscure the owner item.
            Rectangle itemScreenBounds = new Rectangle(TranslatePoint(Point.Empty, ToolStripPointType.ToolStripItemCoords, ToolStripPointType.ScreenCoords), Size);

            if (Rectangle.Intersect(dropDownBounds, itemScreenBounds).Height > 1)
            {

                bool rtl = (RightToLeft == RightToLeft.Yes);

                // try positioning to the left
                if (Rectangle.Intersect(dropDownBounds, itemScreenBounds).Width > 1)
                {
                    dropDownBounds = DropDownDirectionToDropDownBounds(!rtl ? ToolStripDropDownDirection.Right : ToolStripDropDownDirection.Left, dropDownBounds);
                }

                // try positioning to the right
                if (Rectangle.Intersect(dropDownBounds, itemScreenBounds).Width > 1)
                {
                    dropDownBounds = DropDownDirectionToDropDownBounds(!rtl ? ToolStripDropDownDirection.Left : ToolStripDropDownDirection.Right, dropDownBounds);
                }
            }

            return dropDownBounds;

        }

        /// <summary>
        ///  Hides the DropDown, if it is visible.
        /// </summary>
        public void HideDropDown()
        {
            // consider - CloseEventArgs to prevent shutting down.
            OnDropDownHide(EventArgs.Empty);

            if (dropDown != null && dropDown.Visible)
            {
                DropDown.Visible = false;

                AccessibilityNotifyClients(AccessibleEvents.StateChange);
                AccessibilityNotifyClients(AccessibleEvents.NameChange);
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (dropDown != null)
            {
                dropDown.OnOwnerItemFontChanged(EventArgs.Empty);
            }
        }

        protected override void OnBoundsChanged()
        {
            base.OnBoundsChanged();
            //Reset the Bounds...
            if (dropDown != null && dropDown.Visible)
            {
                dropDown.Bounds = GetDropDownBounds(DropDownDirection);
            }
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            if (HasDropDownItems)
            {
                // only perform a layout on a visible dropdown - otherwise clear the preferred size cache.
                if (DropDown.Visible)
                {
                    LayoutTransaction.DoLayout(DropDown, this, PropertyNames.RightToLeft);
                }
                else
                {
                    CommonProperties.xClearPreferredSizeCache(DropDown);
                    DropDown.LayoutRequired = true;
                }
            }
        }

        internal override void OnImageScalingSizeChanged(EventArgs e)
        {
            base.OnImageScalingSizeChanged(e);
            if (HasDropDown && DropDown.IsAutoGenerated)
            {
                DropDown.DoLayoutIfHandleCreated(new ToolStripItemEventArgs(this));
            }
        }

        /// <summary>
        ///  Called as a response to HideDropDown
        /// </summary>
        protected virtual void OnDropDownHide(EventArgs e)
        {
            Invalidate();

            ((EventHandler)Events[EventDropDownHide])?.Invoke(this, e);
        }

        /// <summary>
        ///  Last chance to stick in the DropDown before it is shown.
        /// </summary>
        protected virtual void OnDropDownShow(EventArgs e)
        {
            ((EventHandler)Events[EventDropDownShow])?.Invoke(this, e);
        }

        /// <summary>
        ///  called when the default item is clicked
        /// </summary>
        protected internal virtual void OnDropDownOpened(EventArgs e)
        {
            // only send the event if we're the thing that currently owns the DropDown.

            if (DropDown.OwnerItem == this)
            {
                ((EventHandler)Events[EventDropDownOpened])?.Invoke(this, e);
            }
        }

        /// <summary>
        ///  called when the default item is clicked
        /// </summary>
        protected internal virtual void OnDropDownClosed(EventArgs e)
        {
            // only send the event if we're the thing that currently owns the DropDown.
            Invalidate();

            if (DropDown.OwnerItem == this)
            {
                ((EventHandler)Events[EventDropDownClosed])?.Invoke(this, e);

                if (!DropDown.IsAutoGenerated)
                {
                    DropDown.OwnerItem = null;
                }
            }
        }

        /// <summary>
        ///  called when the default item is clicked
        /// </summary>
        protected internal virtual void OnDropDownItemClicked(ToolStripItemClickedEventArgs e)
        {
            // only send the event if we're the thing that currently owns the DropDown.

            if (DropDown.OwnerItem == this)
            {
                ((ToolStripItemClickedEventHandler)Events[EventDropDownItemClicked])?.Invoke(this, e);
            }
        }

        protected internal override bool ProcessCmdKey(ref Message m, Keys keyData)
        {
            if (HasDropDownItems)
            {
                return DropDown.ProcessCmdKeyInternal(ref m, keyData);
            }
            return base.ProcessCmdKey(ref m, keyData);
        }

        protected internal override bool ProcessDialogKey(Keys keyData)
        {
            Keys keyCode = (Keys)keyData & Keys.KeyCode;

            if (HasDropDownItems)
            {

                // Items on the overflow should have the same kind of keyboard handling as a toplevel
                bool isToplevel = (!IsOnDropDown || IsOnOverflow);

                if (isToplevel && (keyCode == Keys.Down || keyCode == Keys.Up || keyCode == Keys.Enter || (SupportsSpaceKey && keyCode == Keys.Space)))
                {
                    Debug.WriteLineIf(ToolStrip.SelectionDebug.TraceVerbose, "[SelectDBG ProcessDialogKey] open submenu from toplevel item");

                    if (Enabled || DesignMode)
                    {
                        // |__[ * File ]_____|  * is where you are.  Up or down arrow hit should expand menu
                        ShowDropDown();
                        KeyboardToolTipStateMachine.Instance.NotifyAboutLostFocus(this);
                        DropDown.SelectNextToolStripItem(null, true);
                    }// else eat the key
                    return true;

                }
                else if (!isToplevel)
                {

                    // if we're on a DropDown - then cascade out.
                    bool menusCascadeRight = (((int)DropDownDirection & 0x0001) == 0);
                    bool forward = ((keyCode == Keys.Enter) || (SupportsSpaceKey && keyCode == Keys.Space));
                    forward = (forward || (menusCascadeRight && keyCode == Keys.Left) || (!menusCascadeRight && keyCode == Keys.Right));

                    if (forward)
                    {
                        Debug.WriteLineIf(ToolStrip.SelectionDebug.TraceVerbose, "[SelectDBG ProcessDialogKey] open submenu from NON-toplevel item");

                        if (Enabled || DesignMode)
                        {
                            ShowDropDown();
                            KeyboardToolTipStateMachine.Instance.NotifyAboutLostFocus(this);
                            DropDown.SelectNextToolStripItem(null, true);
                        } // else eat the key
                        return true;
                    }

                }
            }

            if (IsOnDropDown)
            {
                bool menusCascadeRight = (((int)DropDownDirection & 0x0001) == 0);
                bool backward = ((menusCascadeRight && keyCode == Keys.Right) || (!menusCascadeRight && keyCode == Keys.Left));

                if (backward)
                {
                    Debug.WriteLineIf(ToolStrip.SelectionDebug.TraceVerbose, "[SelectDBG ProcessDialogKey] close submenu from NON-toplevel item");

                    // we're on a drop down but we're heading back up the chain.
                    // remember to select the item that displayed this dropdown.
                    ToolStripDropDown parent = GetCurrentParentDropDown();
                    if (parent != null && !parent.IsFirstDropDown)
                    {
                        // we're walking back up the dropdown chain.
                        parent.SetCloseReason(ToolStripDropDownCloseReason.Keyboard);
                        KeyboardToolTipStateMachine.Instance.NotifyAboutLostFocus(this);
                        parent.SelectPreviousToolStrip();
                        return true;
                    }
                    // else if (parent.IsFirstDropDown)
                    //    the base handling (ToolStripDropDown.ProcessArrowKey) will perform auto-expansion of
                    //    the previous item in the menu.

                }
            }

            Debug.WriteLineIf(ToolStrip.SelectionDebug.TraceVerbose, "[SelectDBG ProcessDialogKey] ddi calling base");
            return base.ProcessDialogKey(keyData);
        }

        private ToolStripDropDownDirection RTLTranslateDropDownDirection(ToolStripDropDownDirection dropDownDirection, RightToLeft rightToLeft)
        {
            switch (dropDownDirection)
            {
                case ToolStripDropDownDirection.AboveLeft:
                    return ToolStripDropDownDirection.AboveRight;
                case ToolStripDropDownDirection.AboveRight:
                    return ToolStripDropDownDirection.AboveLeft;
                case ToolStripDropDownDirection.BelowRight:
                    return ToolStripDropDownDirection.BelowLeft;
                case ToolStripDropDownDirection.BelowLeft:
                    return ToolStripDropDownDirection.BelowRight;
                case ToolStripDropDownDirection.Right:
                    return ToolStripDropDownDirection.Left;
                case ToolStripDropDownDirection.Left:
                    return ToolStripDropDownDirection.Right;
            }
            Debug.Fail("Why are we here");

            // dont expect it to come to this but just in case here are the real defaults.
            if (IsOnDropDown)
            {
                return (rightToLeft == RightToLeft.Yes) ? ToolStripDropDownDirection.Left : ToolStripDropDownDirection.Right;
            }
            else
            {
                return (rightToLeft == RightToLeft.Yes) ? ToolStripDropDownDirection.BelowLeft : ToolStripDropDownDirection.BelowRight;
            }
        }

        /// <summary>
        ///  Shows the DropDown, if one is set.
        /// </summary>
        public void ShowDropDown()
            => ShowDropDown(false);

        internal void ShowDropDown(bool mousePush)
        {
            ShowDropDownInternal();
            if (dropDown is ToolStripDropDownMenu menu)
            {
                if (!mousePush)
                {
                    menu.ResetScrollPosition();
                }
                menu.RestoreScrollPosition();
            }
        }

        private void ShowDropDownInternal()
        {
            if (dropDown == null || (!dropDown.Visible))
            {
                // We want to show if there's no dropdown
                // or if the dropdown is not visible.
                OnDropDownShow(EventArgs.Empty);
            }

            // the act of setting the drop down visible the first time sets the parent
            // it seems that GetVisibleCore returns true if your parent is null.

            if (dropDown != null && !dropDown.Visible)
            {
                if (dropDown.IsAutoGenerated && DropDownItems.Count <= 0)
                {
                    return;  // this is a no-op for autogenerated drop downs.
                }

                if (DropDown == ParentInternal)
                {
                    throw new InvalidOperationException(SR.ToolStripShowDropDownInvalidOperation);
                }

                dropDown.OwnerItem = this;
                dropDown.Location = DropDownLocation;
                dropDown.Show();
                Invalidate();

                AccessibilityNotifyClients(AccessibleEvents.StateChange);
                AccessibilityNotifyClients(AccessibleEvents.NameChange);
            }
        }

        private bool ShouldSerializeDropDown()
            => dropDown != null && !dropDown.IsAutoGenerated;

        private bool ShouldSerializeDropDownDirection()
            => toolStripDropDownDirection != ToolStripDropDownDirection.Default;

        private bool ShouldSerializeDropDownItems()
            => dropDown != null && dropDown.IsAutoGenerated;

        internal override void OnKeyboardToolTipHook(ToolTip toolTip)
        {
            base.OnKeyboardToolTipHook(toolTip);
            KeyboardToolTipStateMachine.Instance.Hook(DropDown, toolTip);
        }

        internal override void OnKeyboardToolTipUnhook(ToolTip toolTip)
        {
            base.OnKeyboardToolTipUnhook(toolTip);
            KeyboardToolTipStateMachine.Instance.Unhook(DropDown, toolTip);
        }

        internal override void ToolStrip_RescaleConstants(int oldDpi, int newDpi)
        {
            RescaleConstantsInternal(newDpi);

            // Traversing the tree of DropDownMenuItems non-recursively to set new
            // Font (where necessary because not inherited from parent), DeviceDpi and reset the scaling.
            var itemsStack = new Collections.Generic.Stack<ToolStripDropDownItem>();

            itemsStack.Push(this);

            while (itemsStack.Count > 0)
            {
                var item = itemsStack.Pop();

                if (item.dropDown != null)
                {
                    // The following does not get set, since dropDown has no parent/is not part of the
                    // controls collection, so this gets never called through the normal inheritance chain.
                    item.dropDown._deviceDpi = newDpi;
                    item.dropDown.ResetScaling(newDpi);

                    foreach (ToolStripItem childItem in item.DropDown.Items)
                    {
                        if (childItem == null)
                            continue;

                        // Checking if font was inherited from parent.
                        Font local = childItem.Font;
                        if (!local.Equals(childItem.OwnerItem?.Font))
                        {
                            var factor = (float)newDpi / oldDpi;
                            childItem.Font = new Font(local.FontFamily, local.Size * factor, local.Style,
                                                    local.Unit, local.GdiCharSet, local.GdiVerticalFont);
                        }

                        childItem.DeviceDpi = newDpi;

                        if (typeof(ToolStripDropDownItem).IsAssignableFrom(childItem.GetType()))
                        {
                            if (((ToolStripDropDownItem)childItem).dropDown != null)
                            {
                                itemsStack.Push((ToolStripDropDownItem)childItem);
                            }
                        }
                    }
                }
            }

            // It's important to call the base class method only AFTER we processed all DropDown items,
            // because we need the new DPI in place, before a Font change triggers new layout calc.
            base.ToolStrip_RescaleConstants(oldDpi, newDpi);
        }
    }

    [Runtime.InteropServices.ComVisible(true)]
    public class ToolStripDropDownItemAccessibleObject : ToolStripItem.ToolStripItemAccessibleObject
    {
        private readonly ToolStripDropDownItem owner;
        public ToolStripDropDownItemAccessibleObject(ToolStripDropDownItem item) : base(item)
        {
            owner = item;
        }
        public override AccessibleRole Role
        {
            get
            {
                AccessibleRole role = Owner.AccessibleRole;
                if (role != AccessibleRole.Default)
                {
                    return role;
                }
                return AccessibleRole.MenuItem;
            }
        }

        public override void DoDefaultAction()
        {
            if (Owner is ToolStripDropDownItem item && item.HasDropDownItems)
            {
                item.ShowDropDown();
            }
            else
            {
                base.DoDefaultAction();
            }
        }

        internal override bool IsIAccessibleExSupported()
        {
            if (owner != null)
            {
                return true;
            }
            else
            {
                return base.IsIAccessibleExSupported();
            }
        }

        internal override bool IsPatternSupported(UiaCore.UIA patternId)
        {
            if (patternId == UiaCore.UIA.ExpandCollapsePatternId && owner.HasDropDownItems)
            {
                return true;
            }
            else
            {
                return base.IsPatternSupported(patternId);
            }
        }

        internal override object GetPropertyValue(UiaCore.UIA propertyID)
        {
            if (propertyID == UiaCore.UIA.IsOffscreenPropertyId && owner != null && owner.Owner is ToolStripDropDown)
            {
                return !((ToolStripDropDown)owner.Owner).Visible;
            }

            return base.GetPropertyValue(propertyID);
        }

        internal override void Expand()
            => DoDefaultAction();

        internal override void Collapse()
        {
            if (owner != null && owner.DropDown != null && owner.DropDown.Visible)
            {
                owner.DropDown.Close();
            }
        }

        internal override UiaCore.ExpandCollapseState ExpandCollapseState
        {
            get
            {
                return owner.DropDown.Visible ? UiaCore.ExpandCollapseState.Expanded : UiaCore.ExpandCollapseState.Collapsed;
            }
        }

        public override AccessibleObject GetChild(int index)
        {
            if ((owner == null) || !owner.HasDropDownItems)
            {
                return null;
            }
            return owner.DropDown.AccessibilityObject.GetChild(index);
        }

        public override int GetChildCount()
        {
            if ((owner == null) || !owner.HasDropDownItems)
            {
                return -1;
            }

            // Do not expose child items when the submenu is collapsed to prevent Narrator from announcing
            // invisible menu items when Narrator is in item's mode (CAPSLOCK + Arrow Left/Right) or
            // in scan mode (CAPSLOCK + Space)
            if (ExpandCollapseState == UiaCore.ExpandCollapseState.Collapsed)
            {
                return 0;
            }

            if (owner.DropDown.LayoutRequired)
            {
                LayoutTransaction.DoLayout(owner.DropDown, owner.DropDown, PropertyNames.Items);
            }
            return owner.DropDown.AccessibilityObject.GetChildCount();
        }

        internal int GetChildFragmentIndex(ToolStripItem.ToolStripItemAccessibleObject child)
        {
            if ((owner == null) || (owner.DropDownItems == null))
            {
                return -1;
            }

            for (int i = 0; i < owner.DropDownItems.Count; i++)
            {
                if (owner.DropDownItems[i].Available && child.Owner == owner.DropDownItems[i])
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///  Gets the number of children belonging to an accessible object.
        /// </summary>
        /// <returns>The number of children.</returns>
        internal int GetChildFragmentCount()
        {
            if ((owner == null) || (owner.DropDownItems == null))
            {
                return -1;
            }

            int count = 0;
            for (int i = 0; i < owner.DropDownItems.Count; i++)
            {
                if (owner.DropDownItems[i].Available)
                {
                    count++;
                }
            }

            return count;
        }

        internal AccessibleObject GetChildFragment(int index)
        {
            if (owner.DropDown.AccessibilityObject is ToolStrip.ToolStripAccessibleObject toolStripAccessibleObject)
            {
                return toolStripAccessibleObject.GetChildFragment(index);
            }

            return null;
        }

        internal override UiaCore.IRawElementProviderFragment FragmentNavigate(UiaCore.NavigateDirection direction)
        {
            if (owner == null || owner.DropDown == null)
            {
                return null;
            }

            switch (direction)
            {
                case UiaCore.NavigateDirection.NextSibling:
                case UiaCore.NavigateDirection.PreviousSibling:
                    if (!(owner.Owner is ToolStripDropDown dropDown))
                    {
                        break;
                    }
                    int index = dropDown.Items.IndexOf(owner);

                    if (index == -1)
                    {
                        Debug.Fail("No item matched the index?");
                        return null;
                    }

                    index += direction == UiaCore.NavigateDirection.NextSibling ? 1 : -1;

                    if (index >= 0 && index < dropDown.Items.Count)
                    {
                        ToolStripItem item = dropDown.Items[index];
                        if (item is ToolStripControlHost controlHostItem)
                        {
                            return controlHostItem.ControlAccessibilityObject;
                        }

                        return item.AccessibilityObject;
                    }

                    return null;
            }

            return base.FragmentNavigate(direction);
        }
    }
}
