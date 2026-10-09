using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using GameGuild.API.Database;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameGuild.API.Setup;

/// <summary>
/// Certificate-encrypted storage around immutable Data Protection key XML.
/// Existing key IDs, descriptors, lifetimes and revocations remain unchanged.
/// </summary>
internal sealed class CertificateProtectedKeyRepository : IXmlRepository, IDisposable
{
    private static readonly XNamespace StorageNamespace = "urn:platform:data-protection:key-storage:v1";
    private static readonly XName EnvelopeName = StorageNamespace + "protectedKey";
    private static readonly XName XmlPayloadName = StorageNamespace + "keyXml";
    private static readonly XName EncryptedDataName = "{http://www.w3.org/2001/04/xmlenc#}EncryptedData";
    private readonly IServiceProvider _services;
    private readonly X509Certificate2 _certificate;
    private readonly CertificateXmlEncryptor _encryptor;
    private readonly EncryptedXmlDecryptor _decryptor;

    public CertificateProtectedKeyRepository(IServiceProvider services, X509Certificate2 certificate)
    {
        _services = services;
        _certificate = certificate;
        _encryptor = new CertificateXmlEncryptor(certificate, NullLoggerFactory.Instance);
        _decryptor = new EncryptedXmlDecryptor(services);
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return context.Database.CreateExecutionStrategy().Execute(() => ReadElements(context));
    }

    private IReadOnlyCollection<XElement> ReadElements(ApplicationDbContext context)
    {
        // Each retry must read durable rows again. An aborted conversion can leave
        // encrypted values tracked even though the database still contains legacy XML.
        context.ChangeTracker.Clear();
        using var transaction = context.Database.BeginTransaction();
        // Concurrent hosts must read the committed envelope before returning key material.
        // A failed conversion rolls back the entire batch without removing any existing key.
        var rows = context.DataProtectionKeys
            .FromSqlRaw("SELECT \"Id\", \"FriendlyName\", \"Xml\" FROM \"DataProtectionKeys\" ORDER BY \"Id\" FOR UPDATE")
            .ToList();
        var elements = new List<XElement>(rows.Count);
        var changed = false;
        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.Xml))
            {
                throw InvalidStorage();
            }
            var element = Parse(row.Xml);
            if (element.Name == EnvelopeName)
            {
                elements.Add(Unwrap(element));
            }
            else
            {
                if (element.Name.Namespace == StorageNamespace)
                {
                    throw InvalidStorage();
                }
                // Only the repository encoding changes, not the canonical key XML.
                row.Xml = Wrap(element).ToString(SaveOptions.DisableFormatting);
                elements.Add(element);
                changed = true;
            }
        }
        if (changed)
        {
            context.SaveChanges();
        }
        transaction.Commit();
        return elements.AsReadOnly();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        var encrypted = Wrap(element).ToString(SaveOptions.DisableFormatting);
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = friendlyName, Xml = encrypted });
        context.SaveChanges();
    }

    private XElement Wrap(XElement element)
    {
        try
        {
            // XML encryption can normalize namespace declarations. Protect the canonical
            // serialization as text so repository round-trips preserve the complete tree.
            var payload = new XElement(XmlPayloadName, element.ToString(SaveOptions.DisableFormatting));
            return new XElement(EnvelopeName, new XAttribute("version", "1"),
                _encryptor.Encrypt(payload).EncryptedElement);
        }
        catch (Exception exception) when (exception is CryptographicException or XmlException or InvalidOperationException)
        {
            throw InvalidStorage();
        }
    }

    private XElement Unwrap(XElement element)
    {
        var children = element.Elements().ToArray();
        if ((string?)element.Attribute("version") != "1" || children.Length != 1 || children[0].Name != EncryptedDataName)
        {
            throw InvalidStorage();
        }
        try
        {
            var payload = _decryptor.Decrypt(children[0]);
            if (payload.Name != XmlPayloadName || payload.HasElements)
            {
                throw InvalidStorage();
            }
            return Parse(payload.Value);
        }
        catch (Exception exception) when (exception is CryptographicException or XmlException or InvalidOperationException)
        {
            throw InvalidStorage();
        }
    }

    private static XElement Parse(string xml)
    {
        try
        {
            return XElement.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            throw InvalidStorage();
        }
    }

    private static CryptographicException InvalidStorage() =>
        new("Data Protection key storage is invalid or unavailable.");

    public void Dispose() => _certificate.Dispose();
}
