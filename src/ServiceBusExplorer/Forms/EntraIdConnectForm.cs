#region Copyright
//=======================================================================================
// Copyright (c) Microsoft Corporation. All rights reserved.
//=======================================================================================
#endregion

using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.ServiceBus.Messaging;
using ServiceBusExplorer.Auth;
using ServiceBusExplorer.Helpers;

namespace ServiceBusExplorer.Forms
{
    /// <summary>
    /// Modal dialog that lets the user describe an Entra ID-protected Service Bus namespace
    /// (managed identity, DefaultAzureCredential, service principal, interactive browser).
    /// On OK, exposes the namespace details as a pseudo connection string of the form
    /// <c>Endpoint=sb://&lt;fqdn&gt;/;Authentication=...;ClientId=...;TenantId=...;TransportType=...</c>
    /// which is consumed by the rest of ServiceBusExplorer's existing save/connect flow.
    /// </summary>
    public sealed class EntraIdConnectForm : Form
    {
        private readonly ComboBox cboMode;
        private readonly TextBox txtFqdn;
        private readonly TextBox txtClientId;
        private readonly TextBox txtTenantId;
        private readonly TextBox txtClientSecret;
        private readonly TextBox txtCertThumbprint;
        private readonly ComboBox cboTransport;
        private readonly Label lblClientId;
        private readonly Label lblTenantId;
        private readonly Label lblClientSecret;
        private readonly Label lblCertThumbprint;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public string ConnectionString { get; private set; }
        public string FullyQualifiedNamespace { get; private set; }
        public EntraIdAuthenticationOptions AuthenticationOptions { get; private set; }
        public TransportType TransportType { get; private set; }

        public EntraIdConnectForm()
        {
            Text = "Connect with Microsoft Entra ID";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 380);
            Font = new Font("Segoe UI", 9F);

            int y = 14;
            const int labelWidth = 150;
            const int controlLeft = 170;
            const int controlWidth = 330;
            const int rowHeight = 28;

            Controls.Add(NewLabel("Authentication mode:", 12, y, labelWidth));
            cboMode = new ComboBox
            {
                Left = controlLeft,
                Top = y - 3,
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            cboMode.Items.AddRange(new object[]
            {
                AuthModeItem.For(AuthenticationMode.ManagedIdentitySystemAssigned, "Managed Identity (System Assigned) — Azure-hosted only"),
                AuthModeItem.For(AuthenticationMode.ManagedIdentityUserAssigned, "Managed Identity (User Assigned) — Azure-hosted only"),
                AuthModeItem.For(AuthenticationMode.DefaultAzureCredential, "DefaultAzureCredential (az login / VS / env vars)"),
                AuthModeItem.For(AuthenticationMode.ServicePrincipalSecret, "Service Principal (client secret)"),
                AuthModeItem.For(AuthenticationMode.ServicePrincipalCertificate, "Service Principal (certificate)"),
                AuthModeItem.For(AuthenticationMode.InteractiveBrowser, "Interactive browser sign-in"),
            });
            cboMode.SelectedIndex = 2;
            cboMode.SelectedIndexChanged += (s, e) => UpdateFieldVisibility();
            Controls.Add(cboMode);
            y += rowHeight;

            Controls.Add(NewLabel("Fully qualified namespace:", 12, y, labelWidth));
            txtFqdn = NewTextBox(controlLeft, y - 3, controlWidth);
            txtFqdn.TextChanged += (s, e) => UpdateOkEnabled();
            Controls.Add(txtFqdn);
            var hint = new Label
            {
                Left = controlLeft,
                Top = y + 18,
                Width = controlWidth,
                Height = 14,
                ForeColor = SystemColors.GrayText,
                Text = "e.g. mybus.servicebus.windows.net",
                Font = new Font(Font.FontFamily, 8F)
            };
            Controls.Add(hint);
            y += rowHeight + 14;

            lblClientId = NewLabel("Client ID:", 12, y, labelWidth);
            Controls.Add(lblClientId);
            txtClientId = NewTextBox(controlLeft, y - 3, controlWidth);
            txtClientId.TextChanged += (s, e) => UpdateOkEnabled();
            Controls.Add(txtClientId);
            y += rowHeight;

            lblTenantId = NewLabel("Tenant ID:", 12, y, labelWidth);
            Controls.Add(lblTenantId);
            txtTenantId = NewTextBox(controlLeft, y - 3, controlWidth);
            txtTenantId.TextChanged += (s, e) => UpdateOkEnabled();
            Controls.Add(txtTenantId);
            y += rowHeight;

            lblClientSecret = NewLabel("Client secret:", 12, y, labelWidth);
            Controls.Add(lblClientSecret);
            txtClientSecret = NewTextBox(controlLeft, y - 3, controlWidth);
            txtClientSecret.UseSystemPasswordChar = true;
            txtClientSecret.TextChanged += (s, e) => UpdateOkEnabled();
            Controls.Add(txtClientSecret);
            y += rowHeight;

            lblCertThumbprint = NewLabel("Certificate thumbprint:", 12, y, labelWidth);
            Controls.Add(lblCertThumbprint);
            txtCertThumbprint = NewTextBox(controlLeft, y - 3, controlWidth);
            txtCertThumbprint.TextChanged += (s, e) => UpdateOkEnabled();
            Controls.Add(txtCertThumbprint);
            y += rowHeight;

            Controls.Add(NewLabel("Transport type:", 12, y, labelWidth));
            cboTransport = new ComboBox
            {
                Left = controlLeft,
                Top = y - 3,
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false,
            };
            cboTransport.DataSource = new[] { TransportType.Amqp };
            cboTransport.SelectedItem = TransportType.Amqp;
            Controls.Add(cboTransport);
            var transportHint = new Label
            {
                Left = controlLeft,
                Top = y + 18,
                Width = controlWidth,
                Height = 14,
                ForeColor = SystemColors.GrayText,
                Text = "Entra ID / managed identity requires AMQP.",
                Font = new Font(Font.FontFamily, 8F)
            };
            Controls.Add(transportHint);
            y += rowHeight + 14;

            btnOk = new Button
            {
                Text = "OK",
                Left = ClientSize.Width - 180,
                Top = ClientSize.Height - 36,
                Width = 80,
                DialogResult = DialogResult.None,
            };
            btnOk.Click += BtnOk_Click;
            Controls.Add(btnOk);

            btnCancel = new Button
            {
                Text = "Cancel",
                Left = ClientSize.Width - 92,
                Top = ClientSize.Height - 36,
                Width = 80,
                DialogResult = DialogResult.Cancel,
            };
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            UpdateFieldVisibility();
            UpdateOkEnabled();
        }

        private static Label NewLabel(string text, int x, int y, int width)
        {
            return new Label { Left = x, Top = y, Width = width, Text = text, AutoSize = false, Height = 18 };
        }

        private static TextBox NewTextBox(int x, int y, int width)
        {
            return new TextBox { Left = x, Top = y, Width = width };
        }

        private AuthenticationMode SelectedMode =>
            ((AuthModeItem)cboMode.SelectedItem).Mode;

        private void UpdateFieldVisibility()
        {
            var mode = SelectedMode;

            bool needsClientId =
                mode == AuthenticationMode.ManagedIdentityUserAssigned ||
                mode == AuthenticationMode.ServicePrincipalSecret ||
                mode == AuthenticationMode.ServicePrincipalCertificate ||
                mode == AuthenticationMode.DefaultAzureCredential ||
                mode == AuthenticationMode.InteractiveBrowser;

            bool needsTenantId =
                mode == AuthenticationMode.ServicePrincipalSecret ||
                mode == AuthenticationMode.ServicePrincipalCertificate;

            bool tenantOptional =
                mode == AuthenticationMode.DefaultAzureCredential ||
                mode == AuthenticationMode.InteractiveBrowser;

            bool needsSecret = mode == AuthenticationMode.ServicePrincipalSecret;
            bool needsCert = mode == AuthenticationMode.ServicePrincipalCertificate;

            lblClientId.Visible = txtClientId.Visible = needsClientId;
            lblClientId.Text = mode == AuthenticationMode.ManagedIdentityUserAssigned
                ? "Client ID (UAMI):"
                : "Client ID:";

            lblTenantId.Visible = txtTenantId.Visible = needsTenantId || tenantOptional;
            lblTenantId.Text = needsTenantId ? "Tenant ID:" : "Tenant ID (optional):";

            lblClientSecret.Visible = txtClientSecret.Visible = needsSecret;
            lblCertThumbprint.Visible = txtCertThumbprint.Visible = needsCert;

            UpdateOkEnabled();
        }

        private void UpdateOkEnabled()
        {
            if (string.IsNullOrWhiteSpace(txtFqdn.Text))
            {
                btnOk.Enabled = false;
                return;
            }

            var mode = SelectedMode;
            switch (mode)
            {
                case AuthenticationMode.ManagedIdentityUserAssigned:
                    btnOk.Enabled = !string.IsNullOrWhiteSpace(txtClientId.Text);
                    break;
                case AuthenticationMode.ServicePrincipalSecret:
                    btnOk.Enabled = !string.IsNullOrWhiteSpace(txtClientId.Text) &&
                                    !string.IsNullOrWhiteSpace(txtTenantId.Text) &&
                                    !string.IsNullOrWhiteSpace(txtClientSecret.Text);
                    break;
                case AuthenticationMode.ServicePrincipalCertificate:
                    btnOk.Enabled = !string.IsNullOrWhiteSpace(txtClientId.Text) &&
                                    !string.IsNullOrWhiteSpace(txtTenantId.Text) &&
                                    !string.IsNullOrWhiteSpace(txtCertThumbprint.Text);
                    break;
                default:
                    btnOk.Enabled = true;
                    break;
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            try
            {
                var mode = SelectedMode;
                AuthenticationOptions = new EntraIdAuthenticationOptions
                {
                    Mode = mode,
                    ClientId = txtClientId.Visible ? Trim(txtClientId.Text) : null,
                    TenantId = txtTenantId.Visible ? Trim(txtTenantId.Text) : null,
                    ClientSecret = txtClientSecret.Visible ? txtClientSecret.Text : null,
                    CertificateThumbprint = txtCertThumbprint.Visible ? Trim(txtCertThumbprint.Text) : null,
                };

                FullyQualifiedNamespace = NormalizeFqdn(txtFqdn.Text);
                TransportType = (TransportType)cboTransport.SelectedItem;
                ConnectionString = ServiceBusNamespace.BuildEntraIdConnectionString(
                    FullyQualifiedNamespace, AuthenticationOptions, TransportType);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string Trim(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string NormalizeFqdn(string value)
        {
            var v = (value ?? string.Empty).Trim();
            if (v.Contains("://"))
            {
                if (Uri.TryCreate(v, UriKind.Absolute, out var uri))
                {
                    return uri.Host;
                }
            }
            return v.Trim('/');
        }

        private sealed class AuthModeItem
        {
            public AuthenticationMode Mode { get; private set; }
            public string Display { get; private set; }
            public override string ToString() => Display;

            public static AuthModeItem For(AuthenticationMode mode, string display)
            {
                return new AuthModeItem { Mode = mode, Display = display };
            }
        }
    }
}
