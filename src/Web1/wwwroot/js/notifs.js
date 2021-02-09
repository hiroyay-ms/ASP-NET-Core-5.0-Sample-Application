document.addEventListener("DOMContentLoaded", () => {
    var apiBaseUrl = "";

    var fd = new FormData();
    fd.append("paramName", "UserSettings:ApiBaseUrl");

    function getValue()
    {
        $.ajax({
            url: "/Storage/GetConfigurationValue",
            type: "post",
            data: fd,
            async: false,
            processData: false,
            contentType: false,
            success: function (parameterValue) {
                apiBaseUrl = parameterValue;
            },
            error: function () {
                alert("Failed to get the configuration.");
            }
        });
    }

    getValue();

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(apiBaseUrl)
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.on("newMessage", newMessage);
    function newMessage(message) {
        var json = JSON.parse(message);

        alert("SAS Token creation finished!");

        var liElement = document.createElement("li");
        liElement.className = "storage-list-li-result";
        liElement.innerHTML = json.fileName + "<br />" + json.token;

        var ulElement = document.getElementById("result");
        ulElement.removeChild(ulElement.firstElementChild);
        ulElement.style.visibility = "visible";
        ulElement.appendChild(liElement);
    }

    async function start() {
        try {
            await connection.start();
            console.log("SignalR Connected.");
        } catch (err) {
            console.log(err);
            setTimeout(start, 5000);
        }
    };

    connection.onclose(start);

    start();
});