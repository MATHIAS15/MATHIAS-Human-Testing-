﻿CREATE TABLE COMMANDS
(
	ID INTEGER PRIMARY KEY AUTOINCREMENT,
	CMD text NOT NULL
);

INSERT INTO COMMANDS (CMD) VALUES ('HELLO');
INSERT INTO COMMANDS (CMD) VALUES ('EXIT');
INSERT INTO COMMANDS (CMD) VALUES('HUMEUR');
INSERT INTO COMMANDS (CMD) VALUES('READ EMAIL');
INSERT INTO COMMANDS (CMD) VALUES('OFF');
INSERT INTO COMMANDS (CMD) VALUES('ON');
INSERT INTO COMMANDS (CMD) VALUES ('CONTEXT CHANGE')