
CREATE TABLE AppUser (
    Id                  SERIAL NOT NULL,
    SubjectId           TEXT     NOT NULL,
    Username            TEXT     NOT NULL,
    PasswordSalt        TEXT     NOT NULL,
    PasswordHash        TEXT     NOT NULL,
    ProviderName        TEXT     NOT NULL,
    ProviderSubjectId   TEXT     NOT NULL,
    PRIMARY KEY (id)
);

CREATE TABLE Claim (
    Id             SERIAL NOT NULL,
    AppUserId     INT            NOT NULL,
    Issuer         TEXT DEFAULT ('') NOT NULL,
    OriginalIssuer TEXT DEFAULT ('') NOT NULL,
    Subject        TEXT DEFAULT ('') NOT NULL,
    Type           TEXT DEFAULT ('') NOT NULL,
    Value          TEXT DEFAULT ('') NOT NULL,
    ValueType      TEXT DEFAULT ('') NOT NULL,
    PRIMARY KEY (id)
);

CREATE TABLE "grant" (
    Id           SERIAL NOT NULL,
    Key          VARCHAR (200) NOT NULL,
    ClientId     VARCHAR (200) NOT NULL,
    CreationTime TIMESTAMP (6)  NOT NULL,
    Data         TEXT NOT NULL,
    Expiration   TIMESTAMP (6)  NULL,
    SubjectId    VARCHAR (200) NOT NULL,
    Type         VARCHAR (50)  NOT NULL,
    PRIMARY KEY (id)
);