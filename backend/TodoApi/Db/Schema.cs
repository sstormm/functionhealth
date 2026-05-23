using System.Data;
using Dapper;

namespace TodoApi.Db;

public static class Schema
{
    public static void EnsureCreated(IDbConnection conn)
    {
        conn.Execute(@"
CREATE TABLE IF NOT EXISTS Users (
    Id            TEXT    PRIMARY KEY,
    Email         TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash  TEXT    NOT NULL,
    CreatedAt     TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS Sessions (
    Token      TEXT  PRIMARY KEY,
    UserId     TEXT  NOT NULL,
    CreatedAt  TEXT  NOT NULL,
    ExpiresAt  TEXT  NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Sessions_UserId ON Sessions(UserId);

CREATE TABLE IF NOT EXISTS Lists (
    Id         TEXT  PRIMARY KEY,
    UserId     TEXT  NOT NULL,
    Name       TEXT  NOT NULL,
    CreatedAt  TEXT  NOT NULL,
    UpdatedAt  TEXT  NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Lists_UserId ON Lists(UserId);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Lists_UserId_Name ON Lists(UserId, Name COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS Items (
    Id           TEXT     PRIMARY KEY,
    ListId       TEXT     NOT NULL,
    Text         TEXT     NOT NULL,
    Completed    INTEGER  NOT NULL DEFAULT 0,
    ""Order""    INTEGER  NOT NULL DEFAULT 0,
    CompletedAt  TEXT     NULL,
    CreatedAt    TEXT     NOT NULL,
    UpdatedAt    TEXT     NOT NULL,
    FOREIGN KEY (ListId) REFERENCES Lists(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Items_ListId ON Items(ListId);
");
    }
}
