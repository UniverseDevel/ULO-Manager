<#
.SYNOPSIS
    Captures the TLS certificate a ULO camera presents and archives it.

.DESCRIPTION
    Connects to the camera over TLS, prints everything worth recording about the certificate and
    optionally writes it out in DER and PEM form - which is how the files under
    `firmware/<version>/device/` were produced.

    Nothing about the certificate is validated: it cannot be. On firmware 06.0601 it is self-signed
    `CN=localhost`; on 10.1308 it is issued to `CN=*.ulo.camera` by `Mu Design CA`, a private
    authority in no trust store. Both also carry a fixed expiry that the device never renews.

.EXAMPLE
    ./capture_certificate.ps1 -CameraHost 192.0.2.10

.EXAMPLE
    ./capture_certificate.ps1 -CameraHost 192.0.2.10 -OutputFolder ../firmware/10.1308/device
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $CameraHost,
    [int] $Port = 443,
    [string] $OutputFolder,
    [int] $TimeoutMs = 10000
)

$ErrorActionPreference = 'Stop'

function Get-CameraCertificate {
    param([string] $CameraHost, [int] $Port, [int] $TimeoutMs)

    $client = New-Object System.Net.Sockets.TcpClient
    $client.ReceiveTimeout = $TimeoutMs
    $client.SendTimeout = $TimeoutMs
    $client.Connect($CameraHost, $Port)

    # Accept whatever is presented - see the note in the help above.
    $ssl = New-Object System.Net.Security.SslStream($client.GetStream(), $false, { $true })
    try {
        $ssl.AuthenticateAsClient($CameraHost)
        return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($ssl.RemoteCertificate)
    }
    finally {
        $ssl.Dispose()
        $client.Dispose()
    }
}

$certificate = Get-CameraCertificate -CameraHost $CameraHost -Port $Port -TimeoutMs $TimeoutMs
$der = $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)

$sha = [System.Security.Cryptography.SHA256]::Create()
$derHash = [BitConverter]::ToString($sha.ComputeHash($der)).Replace('-', '')
$keyHash = [BitConverter]::ToString($sha.ComputeHash($certificate.PublicKey.EncodedKeyValue.RawData)).Replace('-', '')

Write-Host "Certificate presented by $CameraHost`:$Port" -ForegroundColor Cyan
[pscustomobject]@{
    Subject          = $certificate.Subject
    Issuer           = $certificate.Issuer
    Serial           = $certificate.SerialNumber
    NotBefore        = $certificate.NotBefore.ToUniversalTime().ToString('u')
    NotAfter         = $certificate.NotAfter.ToUniversalTime().ToString('u')
    Signature        = $certificate.SignatureAlgorithm.FriendlyName
    KeySize          = "$($certificate.PublicKey.Key.KeySize) bit"
    Sha1Thumbprint   = $certificate.Thumbprint
    Sha256OfDer      = $derHash
    Sha256OfPublicKey = $keyHash
} | Format-List

Write-Host 'Extensions' -ForegroundColor Cyan
foreach ($extension in $certificate.Extensions) {
    "  {0} [{1}] critical={2}: {3}" -f $extension.Oid.FriendlyName, $extension.Oid.Value, $extension.Critical, $extension.Format($false)
}

# The camera sends only the leaf; the issuing CA is never served, so the chain cannot be built.
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chain.ChainPolicy.RevocationMode = 'NoCheck'
$chain.ChainPolicy.VerificationFlags = 'AllFlags'
[void]$chain.Build($certificate)
Write-Host "Chain elements served by the camera: $($chain.ChainElements.Count)" -ForegroundColor Cyan

if (-not $OutputFolder) {
    Write-Host "`nPass -OutputFolder to write https-server-cert.der and .pem." -ForegroundColor DarkGray
    return
}

New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null
[IO.File]::WriteAllBytes((Join-Path $OutputFolder 'https-server-cert.der'), $der)

$pem = "-----BEGIN CERTIFICATE-----`n" +
       [Convert]::ToBase64String($der, 'InsertLineBreaks') +
       "`n-----END CERTIFICATE-----`n"
[IO.File]::WriteAllText((Join-Path $OutputFolder 'https-server-cert.pem'), $pem)

Write-Host "`nWritten to $OutputFolder" -ForegroundColor Green
Get-ChildItem $OutputFolder -Filter 'https-server-cert.*' | Select-Object Name, Length
