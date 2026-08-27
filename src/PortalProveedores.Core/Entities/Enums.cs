namespace PortalProveedores.Core.Entities;

public enum EstadoFactura
{
    Pendiente = 1,
    Validada = 2,
    Rechazada = 3,
    EnRevision = 4,
    AprobadaParaPago = 5,
    Pagada = 6
}

public enum RolUsuario
{
    Proveedor = 1,
    Revisor = 2,
    Administrador = 3
}
